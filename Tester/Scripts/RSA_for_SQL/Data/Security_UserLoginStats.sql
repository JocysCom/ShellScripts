CREATE TABLE [dbo].[Security_UserLoginStats](
	-- Unique record id.
	[id] int IDENTITY(1,1) NOT NULL,
	-- Profile id. UNIQUE.
	[user_id] int NOT NULL,
	-- Number of invalid logins.
	[invalid_login_count] [int] NOT NULL,
	-- Time until profile is locked.
	[invalid_login_lock_time] [datetime] NULL
 CONSTRAINT [PK_Security_UserLoginStats] PRIMARY KEY CLUSTERED ([id] ASC)
)
GO
