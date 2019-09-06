CREATE PROCEDURE [dbo].[Security_Authorize]
	@username varchar(50),
	@password varchar(100) = null,
AS

-- Failed authorization attempts:
-----  ---------------------------
-- No  Consequences
-----  ---------------------------
-- 1   none
       -- 2   none
       -- 3   invalid_login_lock_time = now +  5 min
       -- 4   invalid_login_lock_time = now + 10 min 
       -- 5   invalid_login_lock_time = now + 20 min 
       -- 6   invalid_login_lock_time = now + 40 min 

-- SQL script formula to calculate lock minutes.
SET @lock_minutes = @invalid_login_lock_minutes * POWER(@invalid_login_lock_multiplier, @invalid_login_count - @invalid_logins_before_lock - 1)
SET @invalid_login_lock_time = DATEADD(MINUTE,@lock_minutes, @now)
