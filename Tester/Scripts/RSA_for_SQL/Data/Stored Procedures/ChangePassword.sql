CREATE PROCEDURE [Security].[ChangePassword]
	@UserName varchar(128),
	@Password nvarchar(max),
	@Security int = NULL,
	@Encrypt bit = 0
AS
SET NOCOUNT ON 

-- EXEC [Security].[ChangePassword] 'user@company.com', 'password123'

---------------------------------------------------------------
-- Get User Id by name.
---------------------------------------------------------------

DECLARE	@UserId bigint = -1
SELECT @UserId = [Id] FROM [Security].Users (NOLOCK) WHERE [Name] = @UserName

IF @UserId = -1
BEGIN
	SELECT -5 AS [StatusCode], 'Your log in credentials are invalid.' AS [StatusText]
	RETURN -5
END

---------------------------------------------------------------

DECLARE
	@cer_name sysname = 'Security_PasswordCertificate01'

-- Base64 hash (512-bit max).
DECLARE @base varchar(176) = [Security].HashPassword(@Password, @Security)
-- Convert base64 string to binary.
DECLARE @salt_hash_bin varbinary(max) = CAST(N'' as xml).value('xs:base64Binary(sql:variable("@base"))', 'varbinary(max)');
-- Get size of salt and hash.
DECLARE @size int = LEN(@salt_hash_bin) / 2
-- Get Salt and Hash (512-bit max).
DECLARE @salt varbinary(64) = SUBSTRING(@salt_hash_bin, 1, @size)
DECLARE @hash varbinary(64) = SUBSTRING(@salt_hash_bin, 1+ @size, @size + @size)


DECLARE @data varbinary(max) = 0x0 

-- Encrypt option intended only during transition period when system still requires to pass original password.
IF @Encrypt = 1
BEGIN
	-- Encrypt password.
	SET @data = EncryptByCert(Cert_ID(@cer_name), @Password)
	--SET @decrypted = DecryptByCert(Cert_ID('SqlTestCertificate01'), @Encrypted, N'Password1234$')
END

PRINT  @base
PRINT  @salt
PRINT  @hash
PRINT  @data

INSERT INTO [Security].[UserPasswords] ([UserId], [Changed], [Salt], [Hash], [Base], [Data])
VALUES (@UserId, GetDate(), @salt, @hash, @base, @data)

SET NOCOUNT OFF

SELECT 0 AS [StatusCode], '' AS [StatusText]
RETURN 0