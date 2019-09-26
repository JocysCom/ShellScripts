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

SETLOCAL
TITLE List Domain Computers
:: %~n0 - filename without extension.
SET file=%~n0
:: Current directory.
SET cdir=%~dp0
:: <script> <working_folder> <pattern> <data_file>
CALL:PS "/domain="
ECHO.
pause
GOTO:EOF

:PS
::@echo on
::SETLOCAL EnableDelayedExpansion
SET pcf=%~dp0powershell.exe.activation_config
SET pse=PowerShell.exe
:: Get PowerShell version.
PowerShell.exe "exit $PSVersionTable.PSVersion.Major"
SET ver=%errorlevel%
ECHO PowerShell v%ver%
:: PS: Create config if PowerShell version is old.
IF "%ver%"=="2" CALL:CFG
IF "%ver%"=="2" SET pse=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe
IF "%ver%"=="2" SET COMPLUS_ApplicationMigrationRuntimeActivationConfigPath=%~dp0
::ECHO pse=%pse%
::ECHO com=%COMPLUS_ApplicationMigrationRuntimeActivationConfigPath%
:: Run script.
SET csFile=%cdir%%file%.cs
SET u1=System.Configuration
SET u2=System.Configuration.Install
SET u3=System.Xml
SET u4=System.Xml.Linq
SET u5=System.Core
SET u6=System.Data
SET u7=System.DirectoryServices
SET u8=System.DirectoryServices.AccountManagement
::"%pse%" ^
::Set-ExecutionPolicy ByPass; ^
::$PSVersionTable;
:: Run script.
"%pse%" ^
Set-ExecutionPolicy RemoteSigned; ^
$source = [IO.File]::ReadAllText('%csFile%'); ^
Add-Type -TypeDefinition "$source" -ReferencedAssemblies @('%u1%','%u2%','%u3%','%u4%','%u5%','%u6%','%u7%','%u8%'); ^
$args = @('%~0', '%~1', '%~2', '%~3', '%~4', '%~5', '%~6', '%~7', '%~8', '%~9'); ^
[%file%]::ProcessArguments($args)
:: PS: Clear configuration property
IF "%ver%"=="2" SET COMPLUS_ApplicationMigrationRuntimeActivationConfigPath=
GOTO:EOF

:CFG
:: Create configuration file which will force powershell to run .NET 4 CLR.
IF EXIST "%pcf%" GOTO:EOF
ECHO Create %pcf%
ECHO.^<?xml version="1.0" encoding="utf-8" ?^>                 > "%pcf%"
ECHO.^<configuration^>                                        >> "%pcf%"
ECHO.  ^<startup useLegacyV2RuntimeActivationPolicy="true"^>  >> "%pcf%"
::ECHO.    ^<supportedRuntime version="v4.0"/^>                 >> "%pcf%"
ECHO.    ^<supportedRuntime version="v4.0.30319"/^>           >> "%pcf%"
ECHO.    ^<supportedRuntime version="v2.0.50727"/^>           >> "%pcf%"
ECHO.  ^</startup^>                                           >> "%pcf%"
ECHO.^</configuration^>                                       >> "%pcf%"
GOTO:EOF
