CREATE PROCEDURE [Security].[AuthorizeUser]
	@UserName varchar(128),
	@RuleName varchar(128) = null,
	@Password nvarchar(max) = null
AS


/*
DELETE [Security].[UserLoginStats]
EXEC [Security].[AuthorizeUser] 'user@company.com', null, 'password123'
*/

SET NOCOUNT ON 

-- Do not display error messages on the website in order to avoid this vulnerability:
-- PT-2018-4: User Enumeration: It is possible to enumerate valid usernames via the login page of the application.

/*
		[Description("")]
		None = 0,
		[Description("Duplicate login name. Please contact IT help desk to sort the issue.")]
		LoginDupe = -1,
		// Description is same as for 'LoginInvalid', to make sure that it is impossible to enumerate valid user names.
		[Description("Login credentials are invalid.")]
		LoginEmpty = -2,
		[Description("Login has been suspended.")]
		LoginSuspended = -3,
		[Description("Account has been suspended.")]
		AccountSuspended = -4,
		[Description("Login credentials are invalid.")]
		LoginInvalid = -5,
		[Description("Could not connect to the database. Please contact the IT department.")]
		ConnectionError = -6,
		[Description("Account is closed.")]
		AccountIsClosed = -7,
		[Description("Account is locked.")]
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
	SELECT -5 AS [StatusCode], 'Supplied password is invalid' AS [StatusText]
	RETURN -5
END

---------------------------------------------------------------
-- Get User and Rule Id by name.
---------------------------------------------------------------

DECLARE
	@UserId bigint = -1,
	@RuleId bigint = -1

SELECT @UserId = [Id] FROM [Security].Users (NOLOCK) WHERE [Name] = @UserName
SELECT @RuleId = [Id] FROM [Security].UserLoginRules (NOLOCK) WHERE [Name] = @RuleName

---------------------------------------------------------------
-- Get user security details
---------------------------------------------------------------

DECLARE
	@now datetime = GetDate(),
	-- Get user details.	
	@user_password_base varchar(max),
	-- User login stats.
	@invalid_login_count int,
	@invalid_login_lock_time DateTime,
	-- Group security settings.
	@invalid_login_lock_minutes int,
	@invalid_login_lock_multiplier int,
	@invalid_logins_before_lock int,
	@invalid_logins_before_suspend int,
	-- User login stats id.
	@StatUserId bigint = null

-- Select details and settings.
SELECT TOP 1
	-- Get user password salt and hash.
	@user_password_base = p.[Base],
	-- Get user login stats.
	@StatUserId = s.UserId,
	@invalid_login_count = ISNULL(s.InvalidLoginCount, 0) + 1,
	@invalid_login_lock_time = s.InvalidLoginLockTime,
	-- Get group settings.
	@invalid_login_lock_minutes = ISNULL(r.InvalidLoginLockMinutes, 5),
	@invalid_login_lock_multiplier = ISNULL(r.InvalidLoginLockMultiplier, 2),
	@invalid_logins_before_lock = ISNULL(r.InvalidLoginsBeforeLock, 2),
	@invalid_logins_before_suspend = ISNULL(r.InvalidLoginsBeforeSuspend, 10)
FROM [Security].[UserPasswords] p
LEFT JOIN [Security].[UserLoginStats] s ON s.[UserId] = @UserId
LEFT JOIN [Security].[UserLoginRules] r ON r.[Id] = @RuleId
WHERE p.[UserId] = @UserId
ORDER BY p.[UserId] ASC, p.[changed] DESC

DECLARE
	@usernameFound int = @@ROWCOUNT,
	@password_is_correct bit = 0

---------------------------------------------------------------
-- If username not found.
---------------------------------------------------------------

IF @usernameFound = 0
BEGIN
	SELECT -5 AS [StatusCode], 'Login is invalid' AS [StatusText]
	RETURN -5
END

---------------------------------------------------------------
-- Check login stats record.
---------------------------------------------------------------

-- If record is missing then insert one.
IF @StatUserId IS NULL
BEGIN
	-- Insert user stats column.
	INSERT INTO [Security].[UserLoginStats] ([UserId], [InvalidLoginCount])
	VALUES (@UserId, 0)
END

---------------------------------------------------------------

PRINT '@UserId = ' + CAST(@UserId AS varchar(21))
PRINT '@invalid_login_lock_minutes = ' + CAST(@invalid_login_lock_minutes AS varchar(11))
PRINT '@invalid_login_lock_multiplier = ' + CAST(@invalid_login_lock_multiplier AS varchar(11))

---------------------------------------------------------------
-- If account is time locked.
---------------------------------------------------------------

DECLARE
	@seconds int,
	@minutes int,
	@timeMessage varchar(200)

-- If account is time locekd then...
IF @invalid_login_lock_minutes > 0 AND @invalid_login_lock_time IS NOT NULL AND @invalid_login_lock_time > @now
BEGIN
	-- Calculate seconds and minutes.
	SET @seconds = DATEDIFF(SECOND, @now , @invalid_login_lock_time)
	SET @minutes = DATEDIFF(MINUTE, @now , @invalid_login_lock_time)
	-- Create status text.
	SET @timeMessage = ' (' + 
	CASE WHEN @seconds < 60
		THEN CAST(@seconds AS varchar(11)) + ' seconds' 
		ELSE CAST(@minutes AS varchar(11)) + ' minutes'
	END + ')'
	-- Select status.
	SELECT
		-9 AS [StatusCode],
		'Account is time locked until ' + FORMAT(@invalid_login_lock_time, 'yyyy-MM-dd HH:mm:ss') + @timeMessage AS [StatusText]
	RETURN -9

END

---------------------------------------------------------------
-- Validate password.
---------------------------------------------------------------

-- Validate password first, because senstive data must be given only if user authenticity is confirmed.

PRINT '@password = ' + QUOTENAME(@password, '''')
PRINT '@user_password_base = ' + QUOTENAME(@user_password_base, '''')

DECLARE @password_is_valid bit = [Security].IsValidPassword(@password, @user_password_base)

PRINT '@password_is_valid = ' + CAST(@password_is_valid AS varchar(max))

---------------------------------------------------------------
-- If password is correct but account is suspended (invalid logins).
---------------------------------------------------------------

DECLARE @is_suspended bit = 0

IF @password_is_valid = 1 AND @is_suspended = 1
BEGIN
	-- Select extra message.
	SELECT
		-9 AS [StatusCode],
		'Account is suspended' AS [StatusText]
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
		-9 AS [StatusCode],
		'Account is disabled' AS [StatusText]
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
		-9 AS [StatusCode],
		'Account is closed' AS [StatusText]
	RETURN -9
END

---------------------------------------------------------------
-- If password is correct
---------------------------------------------------------------

IF @password_is_valid = 1
BEGIN

	UPDATE s SET
		s.InvalidLoginCount = 0,
		s.InvalidLoginLockTime = NULL
	FROM [Security].[UserLoginStats] s
	WHERE s.UserId = @UserId

	SELECT 0 AS [StatusCode], '' AS [StatusText]
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
UPDATE s SET
	-- Increase failed login count.
	s.InvalidLoginCount = @invalid_login_count,
	s.InvalidLoginLockTime = @invalid_login_lock_time
FROM [Security].[UserLoginStats] s
WHERE s.UserId = @UserId

PRINT '@invalid_login_count = ' + CAST(@invalid_login_count AS varchar(max))
PRINT '@invalid_logins_before_lock = ' + CAST(@invalid_logins_before_lock AS varchar(max))
PRINT '@invalid_logins_before_suspend = ' + CAST(@invalid_logins_before_suspend AS varchar(max))
PRINT '@invalid_login_lock_time = ' + CAST(@invalid_login_lock_time AS varchar(max))

---------------------------------------------------------------
-- If no attempts left then...
---------------------------------------------------------------

IF @invalid_login_count > 0 AND ((@invalid_logins_before_suspend - @invalid_login_count) <= 0)
BEGIN

	PRINT 'Lock account'

	-- Set login suspend date.
	UPDATE s SET
		s.InvalidLoginSuspendTime = @invalid_login_lock_time
	FROM [Security].[UserLoginStats] s
	WHERE s.UserId = @UserId

	SELECT -9 AS [StatusCode], 'Invalid login count exceeded. Account was time locked.' AS [StatusText]
	RETURN -9

END

--PRINT @base
--PRINT @isValid

---------------------------------------------------------------
-- If account was time locked just now then...
---------------------------------------------------------------

-- If account is time locekd then...
IF @invalid_login_lock_minutes > 0 AND @invalid_login_lock_time IS NOT NULL AND @invalid_login_lock_time > @now
BEGIN
	-- Calculate seconds and minutes.
	SET @seconds = DATEDIFF(SECOND, @now , @invalid_login_lock_time)
	SET @minutes = DATEDIFF(MINUTE, @now , @invalid_login_lock_time)
	-- Create status text.
	SET @timeMessage = ' (' + 
	CASE WHEN @seconds < 60
		THEN CAST(@seconds AS varchar(11)) + ' seconds' 
		ELSE CAST(@minutes AS varchar(11)) + ' minutes'
	END + ')'
	-- Select status.
	SELECT
		-9 AS [StatusCode],
		'Account was time locked until ' + FORMAT(@invalid_login_lock_time, 'yyyy-MM-dd HH:mm:ss') + @timeMessage AS [StatusText]
	RETURN -9
END

IF @password_is_valid = 0
BEGIN
	SELECT -9 AS [StatusCode], 'Log in credentials are invalid.' AS [StatusText]
	RETURN -9
END

SELECT 0 AS [StatusCode], '' AS [StatusText]
RETURN 0

SET NOCOUNT OFF