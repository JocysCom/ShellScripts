SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Security_UserPasswords](
	-- Unique record id.
	[password_id] int IDENTITY(1,1) NOT NULL,
	-- User/Profile id.
	[user_id] varchar(50) NOT NULL,
	-- Date when password was created.
	[password_change_date] datetime NOT NULL,
	-- Password hash (256-bit)
	[password_hash] varbinary(32) NOT NULL,
	-- Password salt (256-bit)
	[password_salt] varbinary(32) NOT NULL,
 CONSTRAINT [PK_Security_UserPasswords] PRIMARY KEY CLUSTERED ([password_id] ASC)
)
GO
CREATE NONCLUSTERED INDEX [IX_Security_UserPasswords__password_hash__password_salt] ON [dbo].[Security_UserPasswords]
(
	[password_hash] ASC,
	[password_salt] ASC
)
GO
