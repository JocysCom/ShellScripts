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
	-- Updated: 2021-06-09 - Key fix suggestion by NReilingh.
	/*

	-- Use Unicode, because ASCII doesn't work worldwide.
	
	-- Example 1:
	DECLARE @key  varbinary(max) = CAST(N'Password' AS varbinary(max))
	DECLARE @message varbinary(max) = CAST(N'Message' AS varbinary(max))
	SELECT @key AS [Key], @message AS [Message]
	-- Return hash: 0xD28A366CDA742EB767AB56B7B11893EE73BA2CD54792D5ACF9189D54F36E60EE
	SELECT [Security].HMAC('SHA2_256', @key, @message) AS [Hash]

	-- Example 2:
	DECLARE @key  varbinary(max) = 0x63727970746969
	DECLARE @message varbinary(max) = 0x68656c6c6f21
	SELECT @key AS [Key], @message AS [Message]
	-- Return hash: 0xD6CBB383C40418C408740AF2727BF8C08231609A3827DBA31538E0D11AB4B1D4
	SELECT [Security].HMAC('SHA2_256', @key, @message) AS [Hash]

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
		SET @key = SUBSTRING(CAST(@key AS binary(128)), 0, @blockSize);
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
