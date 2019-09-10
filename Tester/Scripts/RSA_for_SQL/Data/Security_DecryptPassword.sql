IF OBJECT_ID('[Security_DecryptPassword]', 'P') IS NOT NULL DROP PROCEDURE [Security_DecryptPassword]

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[Security_DecryptPassword]
	@user_id varchar(50),
	@password nvarchar(max) = 0 OUT,
	@error nvarchar(max) = 0 OUT
AS

/*

DECLARE @password_out nvarchar(max)
DECLARE @error_out nvarchar(max)
EXEC [Security_DecryptPassword] '1', @password_out OUT, @error_out OUT
PRINT 'Password: ' + ISNULL(@password_out, '')
PRINT 'Error: ' + ISNULL(@error_out, '')

*/

SET NOCOUNT ON 

DECLARE
	@cer_name sysname = 'Security_PasswordCertificate01',
	@cer_pass sysname = 'Password1234$',
	@sql nvarchar(max)

DECLARE @data varbinary(max)

SELECT TOP 1 @data = [data] FROM [Security_UserPasswords] WHERE [user_id] = @user_id ORDER BY [user_id] ASC, id DESC

DECLARE @keyType sysname
SELECT @keyType = pvt_key_encryption_type FROM sys.certificates

IF @keyType <> 'PW'
BEGIN
	DECLARE @pvkPath varchar(512) = 'C:\ProgramData\MSSQL\' + DB_NAME() + '.' +@cer_name + '.PrivateKey.pvk'
	-- Importing a private key for a certificate that is already present in the database.
	SET @sql =
		' ALTER CERTIFICATE ' + QUOTENAME(@cer_name) +
		' WITH PRIVATE KEY (' +
		' FILE = ' + QUOTENAME(@pvkPath, '''') +
		', DECRYPTION BY PASSWORD = ' + QUOTENAME(@cer_pass, '''') +
		', ENCRYPTION BY PASSWORD = ' + QUOTENAME(@cer_pass, '''') +
		')'
	PRINT @sql
	EXEC sp_executesql @sql
END

SET @password = DecryptByCert(Cert_ID(@cer_name), @data, @cer_pass)

RETURN -1