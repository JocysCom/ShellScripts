CREATE TABLE [Security].[Users] (
    [Id]      BIGINT         IDENTITY (1, 1) NOT NULL,
    [Name]    NVARCHAR (128) NOT NULL,
    [Created] DATETIME       CONSTRAINT [DF_Users_Created] DEFAULT (getdate()) NOT NULL,
    [Comment] NVARCHAR (MAX) CONSTRAINT [DF_Users_Comment] DEFAULT ('') NOT NULL,
    [Flags]   INT            CONSTRAINT [DF_Users_Flags] DEFAULT ((0)) NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY NONCLUSTERED ([Id] ASC) WITH (FILLFACTOR = 80)
);




GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Binary attributes (enum flags).', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'Flags';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Record comments.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'Comment';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Record created', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'Created';


GO



GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Unique record id.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'Id';


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_Users__Username]
    ON [Security].[Users]([Name] ASC);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Login username.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'Name';

