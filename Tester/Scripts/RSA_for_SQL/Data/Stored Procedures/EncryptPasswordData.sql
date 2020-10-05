CREATE PROCEDURE [Security].[EncryptPasswordData]
	@data nvarchar(max)
AS
SET NOCOUNT ON 

-- EXEC [Security].[EncryptPasswordData] 'password123'

-- Opens the symmetric key for use
OPEN SYMMETRIC KEY SymmetricKey1
DECRYPTION BY CERTIFICATE SqlTestCertificate01
WITH PASSWORD = N'password1234$'

DECLARE @plain2 varchar(max) = 'cleartext';
DECLARE @encrypted2 varbinary(max)
DECLARE @decrypted2 varchar(max)

SELECT @encrypted2 = EncryptByKey(Key_GUID('SymmetricKey1'), @plain2)
SELECT @decrypted2 = DecryptByKey(@encrypted2)

SELECT @plain2, @decrypted2, @encrypted2

-- Closes the symmetric key
CLOSE SYMMETRIC KEY SymmetricKey1