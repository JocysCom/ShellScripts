# HMAC Implementation for Microsoft SQL Server.

- **Security_HMAC** - Implements HMAC algorithm. Supported and tested algorithms: MD2, MD4, MD5, SHA, SHA1, SHA2_256, SHA2_512.
- **Security_HashPassword** - Returns base64 string which contains random salt and password hash inside. Use SHA-256 algorithm.
- **Security_IsValidPassword** - Returns 1 if base64 string and password match. Use SHA-256 algorithm.

Example:
```TSQL
-- Use Unicode, because ASCII doesn't work worldwide.
DECLARE @password nvarchar(max) = N'Password'
-- Get base64 string which contains random salt and password hash inside.
DECLARE @base64 varchar(max) = dbo.Security_HashPassword(@password)
-- Validate password against base64 string.
DECLARE @isValid bit = dbo.Security_IsValidPassword(@password, @base64)
-- Print results.
PRINT '@password: ' + @password
PRINT '@base64:   ' + @base64
PRINT '@isValid:  ' + CAST(@isValid as varchar(1))
```
Prints Results:
```txt
@password: Password
@base64:   QJYy7JRIwEmyicMYUi3DhqD8yPfmBwzDI+2dk6VYcYw=
@isValid:  1
```
<hr />

```TSQL
ALTER FUNCTION [dbo].[Security_HMAC] (
       @algorithm sysname,
       @key varbinary(max),
       @message varbinary(max)
)
RETURNS varbinary(max)
AS
BEGIN
	-- Author: Evaldas Jocys, https://www.jocys.com
	-- Created: 2019-07-25
	/* Example:
	-- Use Unicode, because ASCII doesn't work worldwide.
	DECLARE @key  varbinary(max) = CAST(N'Password' AS varbinary(max))
	DECLARE @message varbinary(max) = CAST(N'Message' AS varbinary(max))
	SELECT @key, @message
	-- Return hash.
	SELECT dbo.Security_HMAC('SHA2_256', @key, @message)
	*/
	-- Set correct block size for the @algorithm: MD2, MD4, MD5, SHA, SHA1, SHA2_256, SHA2_512.
	DECLARE @blockSize int = 64
	IF @algorithm IN ('MD2')
		SET @blockSize = 16
	IF @algorithm IN ('SHA2_512')
		SET @blockSize = 128
	-- If key is longer than block size then...
	IF LEN(@key) > @blockSize 
		-- hash key to make it block size.
		SET @key = CAST(HASHBYTES(@algorithm, @key) AS varbinary(max))
	-- If key is shorter than block size then...
	IF LEN(@key) < @blockSize 
		-- pad it with zeroes on the right.
		SET @key = CAST(@key AS varbinary(max))
	-- Create inner padding.
	DECLARE
		@i int = 1,
		-- Block-sized inner padding.
		@ipad bigint = CAST(0x3636363636363636 AS bigint),
		-- Inner padded key.
		@i_key_pad varbinary(max) = CAST('' AS varbinary(max))
	WHILE @i < @blockSize
	BEGIN
		SET @i_key_pad = @i_key_pad + CAST((SUBSTRING(@key, @i, 8) ^ @ipad) AS varbinary(max))
		SET @i = @i + 8
	END
	-- Create outer padding.
	DECLARE
		@o int = 1,
		-- Block-sized outer padding
		@opad bigint = CAST(0x5C5C5C5C5C5C5C5C AS bigint),
		-- Outer padded key.
		@o_key_pad varbinary(max) = CAST('' AS varbinary(max))
	WHILE @o < @blockSize
	BEGIN
		SET @o_key_pad = @o_key_pad + CAST((SUBSTRING(@key, @o, 8) ^ @opad) AS varbinary(max))
		SET @o = @o + 8
	END
	-- Return keyed hash.
	RETURN HASHBYTES(@algorithm, @o_key_pad + HASHBYTES(@algorithm, @i_key_pad + @message))
END
```
<hr />

View required for Security_HashPassword function in order to generate random salt.

```TSQL
CREATE VIEW [dbo].[Security_NewID]
AS
SELECT NEWID() AS uniqueidentifier
GO
```
<hr />

```TSQL
ALTER FUNCTION [dbo].[Security_HashPassword] (
	@password nvarchar(max),
	@security int
)
RETURNS varchar(max)
AS
BEGIN
	-- Author: Evaldas Jocys, https://www.jocys.com
	-- Created: 2019-07-25
	/* Example:

	Note: @password is Unicode (nvarchar) type, because ASCII doesn't work worldwide.
	
	-- 256-bit security:
	-- Return 64 bytes (32 salt bytes + 32 hash bytes) as base64 string, which will fit into a varchar(88) field.
	DECLARE @base64 nvarchar(max) = dbo.Security_HashPassword(N'Password', null)
	SELECT dbo.Security_IsValidPassword(N'Password', @base64) as [valid], @base64 as [base]

	-- 128-bit security:
	-- Return 32 bytes (16 salt bytes + 16 hash bytes) as base64 string, which will fit into a varchar(44) field.
	DECLARE @base64 nvarchar(max) = dbo.Security_HashPassword(N'Password', 128)
	SELECT dbo.Security_IsValidPassword(N'Password', @base64) as [valid], @base64 as [base]

	*/
	IF @security IS NULL
		SET @security = 256
	DECLARE @size int = @security / 8
	DECLARE @algorithm sysname = 'SHA2_256'
	-- Convert string to bytes.
	DECLARE @password_data varbinary(max) = CAST(@password  AS varbinary(max))
	DECLARE @password_salt varbinary(max) = CAST('' as varbinary(max))
	-- Generate random salt.
	WHILE LEN(@password_salt) < @size
		SET @password_salt = @password_salt + CAST((SELECT * FROM Security_NewID) AS varbinary(16))
	SET @password_salt = SUBSTRING(@password_salt, 1, @size);
	DECLARE @password_hash varbinary(max) = SUBSTRING(dbo.Security_HMAC(@algorithm, @password_salt, @password_data), 1, @size)
	-- Combine salt and hash and convert to HEX.
	DECLARE @salt_hash_bin varbinary(max) = @password_salt + @password_hash
	--DECLARE @salt_hash_hex varchar(max) = CONVERT(varchar(max), @salt_hash_bin, 2)
	-- Convert salt and hash to Base64 string.
	DECLARE @base64 varchar(max) = (SELECT @salt_hash_bin FOR XML PATH(''), BINARY BASE64)
	RETURN @base64
END```
<hr />

```TSQL
ALTER FUNCTION [dbo].[Security_IsValidPassword] (
	@password nvarchar(max),
	@base64 varchar(max)
)
RETURNS bit
AS
BEGIN
	-- Author: Evaldas Jocys, https://www.jocys.com
	-- Created: 2019-07-25
	/* Example:
	-- Use Unicode, because ASCII doesn't work worldwide.
	DECLARE @base64 nvarchar(max) = dbo.Security_HashPassword(N'Password', 128)
	SELECT dbo.Security_IsValidPassword(N'Password', @base64) as [valid], @base64 as [base]
	*/
	-- Convert base64 string to binary.
	DECLARE @salt_hash_bin varbinary(max) = CAST(N'' as xml).value('xs:base64Binary(sql:variable("@base64"))', 'varbinary(max)');
	-- Get size of salt and hash.
	DECLARE @size int = LEN(@salt_hash_bin) / 2
	DECLARE @algorithm sysname = 'SHA2_256'
	-- Salt and Hash size.
	DECLARE @password_data varbinary(max) = CAST(@password AS varbinary(max))
	DECLARE @salt varbinary(max) = SUBSTRING(@salt_hash_bin, 1, @size)
	DECLARE @hash varbinary(max) = SUBSTRING(@salt_hash_bin, 1+ @size, @size + @size)
	DECLARE @password_hash varbinary(max) = SUBSTRING(dbo.Security_HMAC(@algorithm, @salt, @password_data), 1, @size)
	-- If @base64 string, which contains salt and hash, match with supplied password then...
	IF @password_hash = @hash
		RETURN 1
	RETURN 0
END
```
# SQL functions in C#

Example:
```C#
// Use Unicode, because ASCII doesn't work worldwide.
var base64 = HashPassword("Password");
var isValid = IsValidPassword("Password", base64);
Console.WriteLine("Results:");
Console.WriteLine("  IsValid: {0}, base64: {1}", isValid, base64);
return 0;
```
Prints Results:
```txt
HashPassword:
  Salt: 2AEB84618DC799CB4350EC2B8E0F1463
  Hash: 18C1A19172D1A7368A224A041F704248
IsValidPassword:
  Salt: 2AEB84618DC799CB4350EC2B8E0F1463
  Hash: 18C1A19172D1A7368A224A041F704248
Results:
  IsValid: True, base64: KuuEYY3HmctDUOwrjg8UYxjBoZFy0ac2iiJKBB9wQkg=
```


<hr />

```C#
/// <summary>Hash new password.</summary>
public static string HashPassword(string password, int security = 256)
{
	// You can limit security to 128-bit which will produce
	// base64 string, which will fit into a varchar(44) field on the database.
	// This will allow to store encrypted password in old password field if its size is limited.
	var size = security / 8;
	var algorithm = new System.Security.Cryptography.HMACSHA256();
	// ----------------------------------------------------------------
	// Convert string to bytes.
	// Use Unicode, because ASCII doesn't work worldwide and SQL server doesn't support UTF8.
	var bytes = System.Text.Encoding.Unicode.GetBytes(password);
	// Generate random salt.
	var salt = new byte[size];
	var generator = System.Security.Cryptography.RandomNumberGenerator.Create();
	generator.GetBytes(salt);
	// Compute hash.
	algorithm.Key = salt;
	var hash = algorithm.ComputeHash(bytes);
	// Combine salt and hash and convert to HEX.
	var baseBytes = new byte[size * 2];
	Array.Copy(salt, 0, baseBytes, 0, size);
	Array.Copy(hash, 0, baseBytes, size, size);
	Console.WriteLine("HashPassword:");
	Console.WriteLine("  Salt: {0}", string.Join("", salt.Select(x => x.ToString("X2"))));
	Console.WriteLine("  Hash: {0}", string.Join("", hash.Take(size).Select(x => x.ToString("X2"))));
	// Convert salt and hash to Base64 string.
	var base64 = System.Convert.ToBase64String(baseBytes);
	return base64;
}
```
<hr />

```C#
public static bool IsValidPassword(string password, string base64)
{
	// ----------------------------------------------------------------
	if (string.IsNullOrEmpty(password))
		return false;
	if (string.IsNullOrEmpty(base64))
		return false;
	// Try parse salt and hash from base64.
	byte[] baseBytes;
	try { baseBytes = System.Convert.FromBase64String(base64); }
	catch { return false; }
	// Get size of salt and hash.
	var size = baseBytes.Length;
	var salt = new byte[size];
	var hash = new byte[size];
	Array.Copy(baseBytes, 0, salt, 0, size);
	Array.Copy(baseBytes, size, hash, 0, size);
	Console.WriteLine("IsValidPassword:");
	Console.WriteLine("  Salt: {0}", string.Join("", salt.Select(x => x.ToString("X2"))));
	Console.WriteLine("  Hash: {0}", string.Join("", hash.Take(size).Select(x => x.ToString("X2"))));
	// ----------------------------------------------------------------
	// Convert string to bytes.
	// Use Unicode, because ASCII doesn't work worldwide and SQL server doesn't support UTF8.
	var passwordBytes = System.Text.Encoding.Unicode.GetBytes(password);
	var algorithm = new System.Security.Cryptography.HMACSHA256();
	algorithm.Key = salt;
	var passwordHash = algorithm.ComputeHash(passwordBytes);
	// Compare first specified bytes.
	for (int i = 0; i < size; i++)
	{
		if (passwordHash[i] != hash[i])
			// Password hash bytes do not match.
			return false;
	}
	// Password hash bytes match.
	return true;
}
```
