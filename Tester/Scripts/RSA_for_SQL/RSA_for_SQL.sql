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

