@echo OFF
echo Run this script as an Administrator if you get access denied error.
echo.
:: Change disk and go to local folder.
:: Line is needed if script was started as Administrator.
cd /D "%~d0%~p0"
SchTasks.exe /Create /F /XML "%~d0%~p0\IIS_CleanupLogs_Task.xml" /TN "IIS_CleanupLogs_Task"
pause