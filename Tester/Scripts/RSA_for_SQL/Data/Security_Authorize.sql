IF OBJECT_ID('[Security_Authorize]', 'P') IS NOT NULL DROP PROCEDURE [Security_Authorize]

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[Security_Authorize]
	@user_id varchar(50),
	@password nvarchar(50)
AS

-- EXEC [Security_Authorize] '1', 'password123'

SET NOCOUNT ON 

DECLARE @base as varchar(max)

SELECT TOP 1 @base = [base] FROM [Security_UserPasswords] WHERE [user_id] = @user_id ORDER BY [user_id] ASC, id DESC

DECLARE @isValid bit = dbo.Security_IsValidPassword(@password, @base)

PRINT @base
PRINT @isValid

SET NOCOUNT OFF
