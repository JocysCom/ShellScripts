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
:: %~n0 - filename without extension.
SET file=%~n0
:: Current directory.
SET cdir=%~dp0
:: <script> <working_folder> <pattern> <data_file>
CALL:PS "/s=%~0"
ECHO.
pause
GOTO:EOF

:PS
SET vs=c:\Program Files (x86)\Microsoft Visual Studio
SET f1=%vs%\2017\Community\MSBuild\Microsoft\VisualStudio\v15.0\Web\Microsoft.Web.XmlTransform.dll
IF NOT EXIST "%f1%" SET f1=%vs%\2017\Community\MSBuild\Microsoft\VisualStudio\v16.0\Web\Microsoft.Web.XmlTransform.dll
IF NOT EXIST "%f1%" SET f1=%vs%\2017\Professional\MSBuild\Microsoft\VisualStudio\v15.0\Web\Microsoft.Web.XmlTransform.dll
IF NOT EXIST "%f1%" SET f1=%vs%\2017\Professional\MSBuild\Microsoft\VisualStudio\v16.0\Web\Microsoft.Web.XmlTransform.dll
IF NOT EXIST "%f1%" SET f1=%vs%\2017\Enterprise\MSBuild\Microsoft\VisualStudio\v15.0\Web\Microsoft.Web.XmlTransform.dll
IF NOT EXIST "%f1%" SET f1=%vs%\2017\Enterprise\MSBuild\Microsoft\VisualStudio\v16.0\Web\Microsoft.Web.XmlTransform.dll
:: Run script.
SET csFile=%cdir%%file%.cs
SET u1=System.Configuration
SET u2=System.Configuration.Install
SET u3=System.Xml
:: Run script.
PowerShell.exe ^
Set-ExecutionPolicy RemoteSigned; ^
$source = Get-Content -Raw -Path '%csFile%'; ^
Add-Type -TypeDefinition "$source" -ReferencedAssemblies @('%f1%','%u1%','%u2%','%u3%'); ^
$args = @('%~0', '%~1', '%~2', '%~3', '%~4', '%~5', '%~6', '%~7', '%~8', '%~9'); ^
[XML_Transform]::ProcessArguments($args)
GOTO:EOF
