-- Select keys
SELECT [name], 'CERTIFICATE' [encryptor_type] from  sys.certificates UNION
SELECT [name], 'SYMMETRIC KEY' [encryptor_type] from  sys.symmetric_keys

-- Creating a self-signed certificate.
CREATE CERTIFICATE SqlTestCertificate01   
   ENCRYPTION BY PASSWORD = 'password1234$'  
   WITH SUBJECT = 'SqlTestCertificate01',   
   EXPIRY_DATE = '20201031';  
GO  

-- Backup certificate to files.
BACKUP CERTIFICATE SqlTestCertificate01
	TO FILE = 'c:\ProgramData\DAC\SqlTestCertificate01.cer'
	WITH PRIVATE KEY (
		FILE = 'c:\ProgramData\DAC\SqlTestCertificate01.pvk',  
		ENCRYPTION BY PASSWORD = 'password1234$',   
		DECRYPTION BY PASSWORD = 'password1234$'
	);

-- Creating a certificate from a file
CREATE CERTIFICATE SqlTestCertificate01   
    FROM FILE = 'c:\ProgramData\DAC\SqlTestCertificate01.cer'
    WITH PRIVATE KEY (
		FILE = 'c:\ProgramData\DAC\SqlTestCertificate01.pvk',
		DECRYPTION BY PASSWORD = 'password1234$'
	);  
GO 

-- Removing the private key of a certificate.
ALTER CERTIFICATE SqlTestCertificate01 REMOVE PRIVATE KEY  

-- Changing the password that is used to encrypt the private key.
ALTER CERTIFICATE SqlTestCertificate01   
    WITH PRIVATE KEY (DECRYPTION BY PASSWORD = 'password1234$%',  
    ENCRYPTION BY PASSWORD = 'password456$');  
GO

-- Changing the protection of the private key from a password to the database master key.
ALTER CERTIFICATE Shipping15   
    WITH PRIVATE KEY (DECRYPTION BY PASSWORD = '95hk000eEnvjkjy#F%');  
GO 

-- Importing a private key for a certificate that is already present in the database.
ALTER CERTIFICATE SqlTestCertificate01   
    WITH PRIVATE KEY (FILE = 'c:\ProgramData\DAC\SqlTestCertificate01.pvk',  
    DECRYPTION BY PASSWORD = 'password1234$');  
GO  

-- Creating a certificate from a file
CREATE CERTIFICATE SqlTestCertificate01   
    FROM FILE = 'c:\ProgramData\DAC\SqlTestCertificate01.cer'
    WITH PRIVATE KEY (
		FILE = 'c:\ProgramData\DAC\SqlTestCertificate01.pvk',
		DECRYPTION BY PASSWORD = 'password1234$'
	);  
GO 

DECLARE @plain varchar(max) = 'cleartext';
DECLARE @encrypted varbinary(max)
DECLARE @decrypted varchar(max)

SELECT @encrypted = EncryptByCert(Cert_ID('SqlTestCertificate01'), @plain)
SELECT @decrypted = DecryptByCert(Cert_ID('SqlTestCertificate01'), @encrypted, N'password1234$')

SELECT @plain, @decrypted, @encrypted


-- Create symmetric Key and protect with certificate.
CREATE SYMMETRIC KEY SymmetricKey1
WITH ALGORITHM = AES_256
ENCRYPTION BY CERTIFICATE SqlTestCertificate01;

--DROP SYMMETRIC KEY SymmetricKey1

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


-- https://docs.microsoft.com/en-us/sql/t-sql/functions/decryptbykey-transact-sql?view=sql-server-2017
-- https://www.mssqltips.com/sqlservertip/2431/sql-server-column-level-encryption-example-using-symmetric-keys/



