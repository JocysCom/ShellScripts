CREATE TABLE [Security].[UserPasswords] (
    [Id]      BIGINT          IDENTITY (1, 1) NOT NULL,
    [UserId]  BIGINT          NOT NULL,
    [Changed] DATETIME        NOT NULL,
    [Salt]    VARBINARY (64)  NOT NULL,
    [Hash]    VARBINARY (64)  NOT NULL,
    [Base]    VARCHAR (176)   NOT NULL,
    [Data]    VARBINARY (MAX) NOT NULL,
    CONSTRAINT [PK_Security_UserPasswords] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_UserPasswords__UserId__Id]
    ON [Security].[UserPasswords]([UserId] ASC, [Id] DESC);


GO
CREATE NONCLUSTERED INDEX [IX_UserPasswords__Hash__Salt]
    ON [Security].[UserPasswords]([Hash] ASC, [Salt] ASC);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Password encrypted with asymetric encryption.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserPasswords', @level2type = N'COLUMN', @level2name = N'Data';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Base 64 string which contains salt and hash (512-bit).', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserPasswords', @level2type = N'COLUMN', @level2name = N'Base';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Password hash (512-bit)', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserPasswords', @level2type = N'COLUMN', @level2name = N'Hash';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Password salt (512-bit)', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserPasswords', @level2type = N'COLUMN', @level2name = N'Salt';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Date when password was created.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserPasswords', @level2type = N'COLUMN', @level2name = N'Changed';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'User/Profile id.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserPasswords', @level2type = N'COLUMN', @level2name = N'UserId';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Unique record id.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserPasswords', @level2type = N'COLUMN', @level2name = N'Id';

