CREATE FUNCTION [Security].[IsValidPassword] (
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
	DECLARE @algorithm sysname = CASE WHEN @size * 8  > 256 THEN 'SHA2_512' ELSE 'SHA2_256' END
	-- Salt and Hash size.
	DECLARE @password_data varbinary(max) = CAST(@password AS varbinary(max))
	DECLARE @salt varbinary(max) = SUBSTRING(@salt_hash_bin, 1, @size)
	DECLARE @hash varbinary(max) = SUBSTRING(@salt_hash_bin, 1+ @size, @size + @size)
	DECLARE @password_hash varbinary(max) = SUBSTRING([Security].HMAC(@algorithm, @salt, @password_data), 1, @size)
	-- If @base64 string, which contains salt and hash, match with supplied password then...
	IF @password_hash = @hash
		RETURN 1
	RETURN 0
END