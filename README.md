# Usefull Shell Scripts

As a C# developer I like to run pure C# as a script instead of using PowerShell as a proxy.

# How to run pure C# from PowerShell and DOS Batch

Lets say you have HelloWorld.cs script on the disk:
```CSharp
using System;
public class HelloWorld
{
	public static void ProcessArguments(string[] args)
	{
		Console.Write("Hello World!");
	}
}
```

You can execute C# script from HelloWorld.ps1 PowerShell script:
```PS1
param($args);
$source = Get-Content -Raw -Path 'HelloWorld.cs';
Add-Type -TypeDefinition "$source" -ReferencedAssemblies @('System.Configuration','System.Xml');
[HelloWorld]::ProcessArguments($args);
```
You can execute C# script from HelloWorld.bat DOS batch script:
```Batch
@echo off
PowerShell.exe ^
$source = Get-Content -Raw -Path 'HelloWorld.cs'; ^
Add-Type -TypeDefinition "$source" -ReferencedAssemblies @('System.Configuration','System.Xml'); ^
$args = @('%~0', '%~1', '%~2', '%~3', '%~4', '%~5', '%~6', '%~7', '%~8', '%~9'); ^
[HelloWorld]::ProcessArguments($args);
```
You can load custom assemblies (DLLs) files and shedule scripts in Windows Tasks Scheduler.
Basically, use plain C# as a scripting language in Windows.

### [XML Transform...](https://github.com/JocysCom/ShellScripts/tree/master/Tester/Scripts/XML_Transform) 

Transform XML Connfiguration files for different environments like Visual Studio does for Web Application Projects.

### [HMAC Implementation for Microsoft SQL Server...](https://github.com/JocysCom/ShellScripts/tree/master/Tester/Scripts/HMAC_for_SQL)

3 methods implemeted as SQL stored procedure and C# function:
- *Security_HMAC* - Implements HMAC algorithm. Supported and tested algorithms: MD2, MD4, MD5, SHA, SHA1, SHA2_256, SHA2_512.
- *Security_HashPassword* - Returns base64 string which contains random salt and password hash inside. Use SHA-256 algorithm.
- *Security_IsValidPassword* - Returns 1 if base64 string and password match. Use SHA-256 algorithm.

### [List Domain Computers...](https://github.com/JocysCom/ShellScripts/tree/master/Tester/Scripts/List_Domain_Computers)

Export list of domain computers into file with operating system info. Requries "Remote Server Administration Tools for Windows 10" on Windows 10: https://www.microsoft.com/en-au/download/details.aspx?id=45520



## How to checkout as SVN

1. Create Folder C:\Projects\Jocys.com
2. Create _ShellScripts_clone_as_SVN.bat_ file inside this folder with this content:

```batchfile
::-------------------------------------------------------------
:: Checkout project as SVN
::-------------------------------------------------------------
SET company=JocysCom
SET solution=ShellScripts
::-------------------------------------------------------------
SET prg="%ProgramFiles%\TortoiseSVN\bin\svn.exe"
IF NOT EXIST %prg% SET prg="%ProgramFiles(x86)%\TortoiseSVN\bin\svn.exe"
IF NOT EXIST %prg% SET prg="%ProgramW6432%\TortoiseSVN\bin\svn.exe"
:: Checkout Solution.
%prg% checkout https://github.com/%company%/%solution%.git/trunk ".\%solution%"
:: Checkout WIKI pages.
::%prg% checkout https://github.com/%company%/%solution%.wiki.git/trunk "%solution%.wiki"
PAUSE
```

3. Start _ShellScripts_clone_as_SVN.bat_ file.
