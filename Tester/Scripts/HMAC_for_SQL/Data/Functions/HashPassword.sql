CREATE FUNCTION [Security].[HashPassword] (
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
	
	-- 512-bit security:
	-- Return 128 bytes (64 salt bytes + 64 hash bytes) as base64 string, which will fit into a varchar(176) field.
	DECLARE @base64 nvarchar(max) = [Security].HashPassword(N'Password', 512)
	SELECT [Security].IsValidPassword(N'Password', @base64) as [valid], @base64 as [base]

	-- 256-bit security (default):
	-- Return 64 bytes (32 salt bytes + 32 hash bytes) as base64 string, which will fit into a varchar(88) field.
	DECLARE @base64 nvarchar(max) = [Security].HashPassword(N'Password', 256)
	SELECT [Security].IsValidPassword(N'Password', @base64) as [valid], @base64 as [base]

	-- 128-bit security:
	-- Return 32 bytes (16 salt bytes + 16 hash bytes) as base64 string, which will fit into a varchar(44) field.
	DECLARE @base64 nvarchar(max) = [Security].HashPassword(N'Password', 128)
	SELECT [Security].IsValidPassword(N'Password', @base64) as [valid], @base64 as [base]

	*/
	IF @security IS NULL
		SET @security = 256
	DECLARE @size int = @security / 8
	DECLARE @algorithm sysname = CASE WHEN @security > 256 THEN 'SHA2_512' ELSE 'SHA2_256' END
	-- Convert string to bytes.
	DECLARE @password_data varbinary(max) = CAST(@password  AS varbinary(max))
	DECLARE @password_salt varbinary(max) = CAST('' as varbinary(max))
	-- Generate random salt.
	WHILE LEN(@password_salt) < @size
		SET @password_salt = @password_salt + CAST((SELECT * FROM [Security].HashPasswordNewID) AS varbinary(16))
	SET @password_salt = SUBSTRING(@password_salt, 1, @size);
	DECLARE @password_hash varbinary(max) = SUBSTRING([Security].HMAC(@algorithm, @password_salt, @password_data), 1, @size)
	-- Combine salt and hash and convert to HEX.
	DECLARE @salt_hash_bin varbinary(max) = @password_salt + @password_hash
	--DECLARE @salt_hash_hex varchar(max) = CONVERT(varchar(max), @salt_hash_bin, 2)
	-- Convert salt and hash to Base64 string.
	DECLARE @base64 varchar(max) = (SELECT @salt_hash_bin FOR XML PATH(''), BINARY BASE64)
	RETURN @base64
END