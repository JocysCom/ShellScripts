CREATE TABLE [Security].[UserLoginStats] (
    [UserId]                  BIGINT   NOT NULL,
    [InvalidLoginCount]       INT      NOT NULL,
    [InvalidLoginLockTime]    DATETIME NULL,
    [InvalidLoginSuspendTime] DATETIME NULL,
    CONSTRAINT [PK_UserLoginStats_1] PRIMARY KEY CLUSTERED ([UserId] ASC)
);




GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Time until user is locked.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserLoginStats', @level2type = N'COLUMN', @level2name = N'InvalidLoginLockTime';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Number of invalid login attempts.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserLoginStats', @level2type = N'COLUMN', @level2name = N'InvalidLoginCount';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'User id.', @level0type = N'SCHEMA', @level0name = N'Security', @level1type = N'TABLE', @level1name = N'UserLoginStats', @level2type = N'COLUMN', @level2name = N'UserId';


GO


