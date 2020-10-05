
CREATE PROCEDURE [Security].[GeneratePasswordEncryptionKeys]
	@cer_name sysname = 'Security.PasswordCertificate01'
AS
-- EXEC dbo.Security_GenerateKeys

DECLARE
	@cer_pass sysname = 'Password1234$',
	@sql nvarchar(max)

-- If certificate exists then...
IF EXISTS(SELECT * FROM  sys.certificates WHERE [name] = @cer_name)
BEGIN
	-- Drop certificate.
	SET @sql =
		N'DROP CERTIFICATE ' + QUOTENAME(@cer_name)
	PRINT @sql
	-- Creating a self-signed certificate.
	EXEC sp_executesql @sql
END

-- Try to find certificate.
IF NOT EXISTS(SELECT * FROM  sys.certificates WHERE [name] = @cer_name)
BEGIN
	PRINT 'Certificate dropped successfuly'
END

PRINT 'Check files: START'

DECLARE @pvkPath varchar(512) = 'C:\ProgramData\MSSQL\' + DB_NAME() + '.' +@cer_name + '.PrivateKey.pvk'
DECLARE @cerPath varchar(512) = 'C:\ProgramData\MSSQL\' + DB_NAME() + '.' +@cer_name + '.PublicKey.cer'
DECLARE @pvkExists INT
DECLARE @cerExists INT
EXEC master.dbo.xp_fileexist @pvkPath, @pvkExists OUTPUT
EXEC master.dbo.xp_fileexist @cerPath, @cerExists OUTPUT
PRINT @pvkPath
PRINT @pvkExists
PRINT @cerPath
PRINT @cerExists
DECLARE @backupExists bit = @pvkExists & @cerExists

PRINT 'Check files: END'

IF @backupExists = 1
BEGIN
	-- Creating a certificate from a file
	SET @sql = 
	N'CREATE CERTIFICATE ' + QUOTENAME(@cer_name) +
	' FROM FILE = ' + QUOTENAME(@cerPath, '''') +
	' WITH PRIVATE KEY (' +
	' FILE = ' + QUOTENAME(@pvkPath, '''') +
	', DECRYPTION BY PASSWORD = ' + QUOTENAME(@cer_pass, '''') +
	', ENCRYPTION BY PASSWORD = ' + QUOTENAME(@cer_pass, '''') +
	')'
	PRINT @sql
	-- Creating a self-signed certificate.
	EXEC sp_executesql @sql
END

-- If certificate do not exists then...
IF NOT EXISTS(SELECT * FROM  sys.certificates WHERE [name] = @cer_name)
BEGIN
	-- Generate new certificate.
	DECLARE @startd date = GetDate()
	DECLARE @expiry date = DATEADD(YEAR, 10, @startd)
	SET @sql =
		N'CREATE CERTIFICATE ' + QUOTENAME(@cer_name) +
		' ENCRYPTION BY PASSWORD = ' + QUOTENAME(@cer_pass, '''') +
		' WITH SUBJECT = ' + QUOTENAME(@cer_name, '''') + ',' +
		' START_DATE = ' + QUOTENAME(@startd, '''') + ',' +
		' EXPIRY_DATE = ' + QUOTENAME(@expiry, '''') 
	PRINT @sql
	-- Creating a self-signed certificate.
	EXEC sp_executesql @sql
END

-- If certificate xists but no backup then...
IF EXISTS(SELECT * FROM  sys.certificates WHERE [name] = @cer_name) AND @backupExists = 0
BEGIN
	SET @sql =
		'BACKUP CERTIFICATE ' + QUOTENAME(@cer_name) +
		'TO FILE = ' + QUOTENAME(@cerPath, '''') + 
		'WITH PRIVATE KEY (' +
		'	FILE = ' + QUOTENAME(@pvkPath, '''') +
		',	ENCRYPTION BY PASSWORD = ' + QUOTENAME(@cer_pass, '''') +
		',	DECRYPTION BY PASSWORD = ' + QUOTENAME(@cer_pass, '''') +
		')'
	PRINT @sql
	-- Backup certificate to files.
	EXEC sp_executesql @sql
END

---------------------------------------------------------------

-- Removing the private key of a certificate.
SET @sql =
	'ALTER CERTIFICATE ' + QUOTENAME(@cer_name) +
	' REMOVE PRIVATE KEY'
PRINT @sql
EXEC sp_executesql @sql

---------------------------------------------------------------

-- Select keys
SELECT * from  sys.certificates

RETURN 0