@echo off
::-------------------------------------------------------------
:: Check permissions and run as Administrator.
::-------------------------------------------------------------
ATTRIB %windir%\system32 -h | FINDSTR /I "denied" >nul
IF NOT ERRORLEVEL 1 GOTO:ADM
GOTO:EXE
::-------------------------------------------------------------
:ADM
::-------------------------------------------------------------
:: Create temp batch.
SET tb="%TEMP%\%~n0.tmp.bat"
SET tj="%TEMP%\%~n0.tmp.js"
ECHO @echo off> %tb%
ECHO %~d0>> %tb%
ECHO cd "%~p0">> %tb%
ECHO call "%~nx0" %1 %2 %3 %4 %5 %6 %7 %8 %9>> %tb%
ECHO del %tj%>> %tb%
:: Delete itself without generating any error message.
ECHO (goto) 2^>nul ^& del %tb%>> %tb%
:: Create temp script.
ECHO var arg = WScript.Arguments;> %tj%
ECHO var wsh = WScript.CreateObject("WScript.Shell");>> %tj%
ECHO var sha = WScript.CreateObject("Shell.Application");>> %tj%
ECHO sha.ShellExecute(arg(0), "", wsh.CurrentDirectory, "runas", 1);>> %tj%
:: Execute as Administrator.
cscript /B /NoLogo %tj% %tb%
GOTO:EOF
::-------------------------------------------------------------
:EXE
::-------------------------------------------------------------

Powershell.exe -executionpolicy remotesigned -File "%~dp0%~n0.ps1"
ECHO.
pause
GOTO:EOF
