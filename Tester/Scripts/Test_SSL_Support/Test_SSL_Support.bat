@echo off
SETLOCAL
TITLE Test SSL Support
:: %~n0 - filename without extension.
SET file=%~n0
:: Current directory.
SET cdir=%~dp0
:: Test SSL/TLS.
CALL:PS www.google.com 443
CALL:PS smtp.gmail.com 465
:: Test StartTLS.
::CALL:PS mail.jocys.com 110
::CALL:PS mail.jocys.com  25
:: Test LDAP
::CALL:PS ServerName   636  :: LDAP using Active Domain
::CALL:PS ServerName  3269  :: LDAP using Global Catalog
::CALL:PS ServerName  3389  :: Remote Desktop Protocol (RDP):
ECHO.
pause
GOTO:EOF

:PS
:: Run script.
SET csFile=%cdir%%file%.cs
SET u1=System.Configuration
SET u2=System.Configuration.Install
SET u3=System.Xml
:: Run script.
PowerShell.exe -ExecutionPolicy Bypass; ^
$source = Get-Content -Raw -Path '%csFile%'; ^
Add-Type -TypeDefinition "$source" -ReferencedAssemblies @('%u1%','%u2%','%u3%'); ^
$args = @('%~0', '%~1', '%~2', '%~3', '%~4', '%~5', '%~6', '%~7', '%~8', '%~9'); ^
[%file%]::ProcessArguments($args)
GOTO:EOF
