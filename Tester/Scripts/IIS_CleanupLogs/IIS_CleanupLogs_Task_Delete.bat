:: Change disk and go to local folder.
:: Line is needed if script was started as Administrator.
cd /D "%~d0%~p0"
SchTasks.exe /Delete /TN "IIS_CleanupLogs_Task"
pause