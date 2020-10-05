CREATE TABLE [Security].[UserLoginRules] (
    [Id]                         BIGINT         IDENTITY (1, 1) NOT NULL,
    [Name]                       NVARCHAR (128) NOT NULL,
    [PasswordFormat]             NVARCHAR (500) NOT NULL,
    [PasswordFormatError]        NVARCHAR (500) NOT NULL,
    [PasswordChangeDays]         INT            NOT NULL,
    [InactivityDays]             INT            NOT NULL,
    [InvalidLoginLockMinutes]    INT            CONSTRAINT [DF_UserLoginRules_InvalidLoginLockMinutes] DEFAULT ((5)) NOT NULL,
    [InvalidLoginLockMultiplier] INT            CONSTRAINT [DF_UserLoginRules_InvalidLoginLockMultiplier] DEFAULT ((2)) NOT NULL,
    [InvalidLoginsBeforeLock]    INT            CONSTRAINT [DF_UserLoginRules_InvalidLoginsBeforeLock] DEFAULT ((2)) NOT NULL,
    [InvalidLoginsBeforeSuspend] INT            CONSTRAINT [DF_UserLoginRules_InvalidLoginsBeforeSuspend] DEFAULT ((10)) NOT NULL,
    [OldPasswords]               INT            NOT NULL,
    CONSTRAINT [PK_UserLoginRules] PRIMARY KEY CLUSTERED ([Id] ASC)
);




GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'How many old password hashes to keep in [UserPasswords] table.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserLoginRules', @level2type = N'COLUMN', @level2name = N'OldPasswords';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Invalid logins before profile gets suspended. Needs manual reactivation by call centre.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserLoginRules', @level2type = N'COLUMN', @level2name = N'InvalidLoginsBeforeSuspend';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Invalid authorization attempts before profile starts to time lock.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserLoginRules', @level2type = N'COLUMN', @level2name = N'InvalidLoginsBeforeLock';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'How many times lock time must increase after unsuccessful authorization attempt.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserLoginRules', @level2type = N'COLUMN', @level2name = N'InvalidLoginLockMultiplier';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Default lock time.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserLoginRules', @level2type = N'COLUMN', @level2name = N'InvalidLoginLockMinutes';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Number of days before inactive profile is disabled.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserLoginRules', @level2type = N'COLUMN', @level2name = N'InactivityDays';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Number of days before password expires.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserLoginRules', @level2type = N'COLUMN', @level2name = N'PasswordChangeDays';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Password format message if it fails validate against regular expression.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserLoginRules', @level2type = N'COLUMN', @level2name = N'PasswordFormatError';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Password format as regular expression string.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserLoginRules', @level2type = N'COLUMN', @level2name = N'PasswordFormat';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Rule name.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserLoginRules', @level2type = N'COLUMN', @level2name = N'Name';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Unique record id.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserLoginRules', @level2type = N'COLUMN', @level2name = N'Id';

