CREATE TABLE [dbo].[Security_UserPasswords](
	-- Unique record id.
	[id] int IDENTITY(1,1) NOT NULL,
	-- User/Profile id.
	[user_id] varchar(50) NOT NULL,
	-- Date when password was created.
	[changed] datetime NOT NULL,
	-- Password salt (256-bit)
	[salt] varbinary(32) NOT NULL,
	-- Password hash (256-bit)
	[hash] varbinary(32) NOT NULL,
	-- Base 64 string which contains salt and hash (256-bit).
	[base] varchar(88) NOT NULL,
	-- RSA 4096 encrypted password (128-bit security).
	[data] varbinary(512) NOT NULL,
 CONSTRAINT [PK_Security_UserPasswords] PRIMARY KEY CLUSTERED ([id] ASC)
)
GO
CREATE NONCLUSTERED INDEX [IX_Security_UserPasswords__hash__salt] ON [dbo].[Security_UserPasswords]
(
	[hash] ASC,
	[salt] ASC
)
CREATE NONCLUSTERED INDEX [IX_Security_UserPasswords__user_id__id] ON [dbo].[Security_UserPasswords]
(
	[user_id] ASC,
	[id] DESC
)
GO
