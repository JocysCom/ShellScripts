SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Security_Groups](
	-- Unique record id.
	[id] int IDENTITY(1,1) NOT NULL,
	-- Group/Account id. UNIQUE.
	[group_id] char(8) NOT NULL,
	-- Password format as regular expression string.
	[password_format] varchar(500) NOT NULL,
	-- Password format message if it fails validate against regular expression.
	[password_format_error] varchar(500) NOT NULL,
	-- Number of days before password expires.
	[password_change_days] int NOT NULL,
	-- Number of days before inactive profile is disabled.
	[inactivity_days] int NOT NULL,
	-- Default lock time. Default: 5 minutes.
	[invalid_login_lock_minutes] int NOT NULL,
	-- How many times lock time must increase after unsuccessful authorization attempt. Default: 2.
	[invalid_login_lock_multiplier] int NOT NULL,
	-- Invalid authorization attempts before profile starts to lock. Default: 2.
	[invalid_logins_before_lock] int NOT NULL,
	-- Invalid logins before profile gets suspended. Needs manual reactivation by call centre. Default: 10.
	[invalid_logins_before_suspend] int NOT NULL,
	-- How many old password hashes keep in [profile_passwords] table.
	[old_passwords] int NOT NULL
 CONSTRAINT [PK_Security_Groups] PRIMARY KEY CLUSTERED ([id] ASC)
)
GO
