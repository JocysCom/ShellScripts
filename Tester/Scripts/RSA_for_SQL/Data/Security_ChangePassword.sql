IF OBJECT_ID('[Security_ChangePassword]', 'P') IS NOT NULL DROP PROCEDURE [Security_ChangePassword]

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[Security_ChangePassword]
	@user_id varchar(max),
	@password nvarchar(max)
AS
SET NOCOUNT ON 

-- EXEC [Security_ChangePassword] '1', 'password123'

DECLARE
	@cer_name sysname = 'Security_PasswordCertificate01'

DECLARE @base varchar(max) = dbo.Security_HashPassword(@password, null)
-- Convert base64 string to binary.
DECLARE @salt_hash_bin varbinary(max) = CAST(N'' as xml).value('xs:base64Binary(sql:variable("@base"))', 'varbinary(max)');
-- Get size of salt and hash.
DECLARE @size int = LEN(@salt_hash_bin) / 2
-- Get Salt and Hash.
DECLARE @salt varbinary(32) = SUBSTRING(@salt_hash_bin, 1, @size)
DECLARE @hash varbinary(32) = SUBSTRING(@salt_hash_bin, 1+ @size, @size + @size)
-- Encrypt password.

DECLARE @data varbinary(512) = EncryptByCert(Cert_ID(@cer_name), @password)
--SELECT @decrypted = DecryptByCert(Cert_ID('SqlTestCertificate01'), @encrypted, N'Password1234$')

PRINT  @base
PRINT  @salt
PRINT  @hash
PRINT  @data

INSERT INTO [dbo].[Security_UserPasswords] ([user_id], [changed], [salt], [hash], [base], [data])
VALUES (@user_id, GetDate(), @salt, @hash, @base, @data)

SET NOCOUNT OFF
