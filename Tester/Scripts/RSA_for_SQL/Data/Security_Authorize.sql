IF OBJECT_ID('[Security_Authorize]', 'P') IS NOT NULL DROP PROCEDURE [Security_Authorize]

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[Security_Authorize]
	@user_id varchar(50),
	@group_id varchar(50) = null,
	@password nvarchar(max) = null
AS

-- EXEC [Security_Authorize] '1', '1', 'password123'

SET NOCOUNT ON 

-- Do not display error messages on the website in order to avoid this vulnerability:
-- PT-2018-4: User Enumeration: It is possible to enumerate valid usernames via the login page of the application.

/*
		[Description("")]
		None = 0,
		[Description("Duplicate login name. Please contact IT help desk to sort the issue.")]
		LoginDupe = -1,
		// Description is same as for 'LoginInvalid', to make sure that it is impossible to enumerate valid user names.
		[Description("Your log in credentials are invalid.")]
		LoginEmpty = -2,
		[Description("Your profile has been suspended.")]
		LoginSuspended = -3,
		[Description("Your account has been suspended.")]
		AccountSuspended = -4,
		[Description("Your log in credentials are invalid.")]
		LoginInvalid = -5,
		[Description("Could not connect to the database. Please contact the IT department.")]
		ConnectionError = -6,
		[Description("Your profile is closed.")]
		AccountIsClosed = -7,
		[Description("Your profile is locked.")]
		AccountIsLocked = -8,
		[Description("")]
		CustomError = -9,
*/

---------------------------------------------------------------
-- Check for empty password.
---------------------------------------------------------------

-- Fix password.
SET @password = RTRIM(LTRIM(ISNULL(@password, '')))

-- If passwords not supplied then...
IF @password = ''
BEGIN
	SELECT -5 AS [error_code], 'Supplied password is invalid' AS [error_message]
	RETURN -5
END

---------------------------------------------------------------
-- Get user security details
---------------------------------------------------------------

DECLARE
	@now datetime = GetDate(),
	-- Get user details.	
	@user_password nvarchar(50),
	@user_password_base varchar(max),
	-- User login stats.
	@invalid_login_count int,
	@invalid_login_lock_time DateTime,
	-- Group security settings.
	@invalid_login_lock_minutes int,
	@invalid_login_lock_multiplier int,
	@invalid_logins_before_lock int,
	@invalid_logins_before_suspend int

-- Select details and settings.
SELECT TOP 1
	-- Get user password salt and hash.
	@user_password_base = p.[base],
	-- Get user login stats.
	@invalid_login_count = ISNULL(u.invalid_login_count, 0) + 1,
	@invalid_login_lock_time = u.invalid_login_lock_time,
	-- Get group settings.
	@invalid_login_lock_minutes = ISNULL(g.invalid_login_lock_minutes, 5),
	@invalid_login_lock_multiplier = ISNULL(g.invalid_login_lock_multiplier, 2),
	@invalid_logins_before_lock = ISNULL(g.invalid_logins_before_lock, 2),
	@invalid_logins_before_suspend = ISNULL(g.invalid_logins_before_suspend, 10)
FROM [Security_UserPasswords] p
LEFT JOIN [Security_UserLoginStats] u ON u.[user_id] = @user_id
LEFT JOIN [Security_Groups] g ON g.group_id = @group_id
WHERE p.[user_id] = @user_id
ORDER BY p.[user_id] ASC, p.[changed] DESC

DECLARE
	@usernameFound int = @@ROWCOUNT,
	@password_is_correct bit = 0

---------------------------------------------------------------
-- If username not found.
---------------------------------------------------------------

IF @usernameFound = 0
BEGIN
	SELECT -5 AS [error_code], 'Login is invalid' AS [error_message]
	RETURN -5
END

---------------------------------------------------------------

PRINT '@invalid_login_lock_minutes = ' + CAST(@invalid_login_lock_minutes AS varchar(11))
PRINT '@invalid_login_lock_multiplier = ' + CAST(@invalid_login_lock_multiplier AS varchar(11))

---------------------------------------------------------------
-- If account is time locked.
---------------------------------------------------------------

-- If account is locekd then...
IF @invalid_login_lock_minutes > 0 AND @invalid_login_lock_time IS NOT NULL AND @invalid_login_lock_time > @now
BEGIN

	DECLARE @seconds int = DATEDIFF(SECOND, @now , @invalid_login_lock_time)
	DECLARE @minutes int = DATEDIFF(MINUTE, @now , @invalid_login_lock_time)

	DECLARE @timeMessage varchar(200) = ' (' + 
	CASE WHEN @seconds < 60
		THEN CAST(@seconds AS varchar(11)) + ' seconds' 
		ELSE CAST(@minutes AS varchar(11)) + ' minutes'
	END + ')'

	-- Select error message
	SELECT
		-9 AS [error_code],
		'Your account is time locked until ' + FORMAT(@invalid_login_lock_time, 'yyyy-MM-dd HH:mm:ss') + @timeMessage AS [error_message]
	RETURN -9

END

---------------------------------------------------------------
-- Validate password.
---------------------------------------------------------------

-- Validate password first, because senstive data must be given only if user authenticity is confirmed.

PRINT '@password = ' + QUOTENAME(@password, '''')
PRINT '@user_password_base = ' + QUOTENAME(@user_password_base, '''')

DECLARE @password_is_valid bit = dbo.Security_IsValidPassword(@password, @user_password_base)

PRINT '@password_is_valid = ' + CAST(@password_is_valid AS varchar(max))

---------------------------------------------------------------
-- If password is correct but account is suspended (invalid logins).
---------------------------------------------------------------

DECLARE @is_suspended bit = 0

IF @password_is_valid = 1 AND @is_suspended = 1
BEGIN
	-- Select extra message.
	SELECT
		-9 AS [error_code],
		'Account is suspended' AS [error_message]
	RETURN -9
END

---------------------------------------------------------------
-- If password is correct but account is disabled.
---------------------------------------------------------------

DECLARE @is_disabled bit = 0

IF @password_is_valid = 1 AND @is_disabled = 1
BEGIN
	-- Select extra message.
	SELECT
		-9 AS [error_code],
		'Account is disabled' AS [error_message]
	RETURN -9
END

---------------------------------------------------------------
-- If password is correct but account is closed.
---------------------------------------------------------------

DECLARE @is_closed bit = 0

IF @password_is_valid = 1 AND @is_closed = 1
BEGIN
	-- Select extra message.
	SELECT
		-9 AS [error_code],
		'Account is closed' AS [error_message]
	RETURN -9
END

---------------------------------------------------------------
-- If password is correct
---------------------------------------------------------------

IF @password_is_valid = 1
BEGIN

	UPDATE [u] SET
		valid_login_date = @now,
		valid_login_count = valid_login_count + 1,
		invalid_login_count = 0,
		invalid_login_lock_time = NULL
	FROM [Security_UserLoginStats] u
	WHERE u.[user_id] = @user_id

	SELECT 0 AS [error_code], '' AS [error_message]
	RETURN 0
	
END

---------------------------------------------------------------
-- If login is not allowed then...
---------------------------------------------------------------

PRINT 'Time lock account'

-- Reset lock time.
SET @invalid_login_lock_time = NULL
DECLARE @lock_minutes int = 0

-- If record must be locked and user tried more time than allowed then...
IF @invalid_login_lock_minutes > 0 AND @invalid_login_count > @invalid_logins_before_lock
BEGIN

	SET @lock_minutes = @invalid_login_lock_minutes
	IF (@invalid_login_lock_multiplier > 0)
	BEGIN
		SET @lock_minutes = @lock_minutes *
			POWER(@invalid_login_lock_multiplier, @invalid_login_count - @invalid_logins_before_lock - 1)
	END
	SET @invalid_login_lock_time = DATEADD(MINUTE,@lock_minutes, @now)
END

-- Increase invalid login counter.
UPDATE u SET
	-- Increase failed login count.
	invalid_login_count = @invalid_login_count,
	invalid_login_lock_time = @invalid_login_lock_time
FROM [Security_UserLoginStats] u
WHERE u.[user_id] = @user_id

-- If record is missing then insert one.
IF @@ROWCOUNT = 0
BEGIN
	-- Insert invalid login counter.
	INSERT INTO [Security_UserLoginStats] ([user_id], [invalid_login_count])
	VALUES (@user_id, @invalid_login_count)
END

PRINT '@invalid_login_count = ' + CAST(@invalid_login_count AS varchar(max))
PRINT '@invalid_logins_before_lock = ' + CAST(@invalid_logins_before_lock AS varchar(max))
PRINT '@invalid_logins_before_suspend = ' + CAST(@invalid_logins_before_suspend AS varchar(max))

---------------------------------------------------------------
-- If no attempts left then...
---------------------------------------------------------------

IF @invalid_login_count > 0 AND ((@invalid_login_count - @invalid_logins_before_suspend) <= 0)
BEGIN

	PRINT 'Lock account'

	UPDATE [profile] SET
		login_disabled='Y',
		closed_date=@now,
		modify_account='91000',
		modify_uid='SYSADM',
		closed_account='91000',
		closed_uid='SYSADM'
	WHERE [account] = @account AND [uid] = @uid AND
		-- Only write if the profile is not already disabled.
		login_disabled <> 'Y'

	SELECT -9 AS [error_code], 'Invalid login count exceeded. Your account was locked.' AS [error_message]
	RETURN -9

END



--PRINT @base
--PRINT @isValid

SELECT 0 AS [error_code], '' AS [error_message]
RETURN 0

SET NOCOUNT OFF
