IF OBJECT_ID('[Security_EncryptData]', 'P') IS NOT NULL DROP PROCEDURE [Security_EncryptData]

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[Security_EncryptData]
	@data nvarchar(max)
AS
SET NOCOUNT ON 

-- EXEC [Security_EncryptData] 'password123'



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

