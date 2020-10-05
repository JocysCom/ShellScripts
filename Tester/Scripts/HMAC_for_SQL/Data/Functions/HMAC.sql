CREATE FUNCTION [Security].[HMAC] (
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
	SELECT [Security].HMAC('SHA2_256', @key, @message)
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