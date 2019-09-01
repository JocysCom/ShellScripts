# Usefull Shell Scripts

As a C# developer I like to run pure C# as a script instead of using PowerShell as a proxy. :)

## XML Transform - [Wiki...](https://github.com/JocysCom/ShellScripts/wiki/XML-Transform) 

Transform XML Connfiguration files for different environments like Visual Studio does for Web Application Projects.

## List Domain Computers

Export list of domain computers into file with operating system info.

Requries "Remote Server Administration Tools for Windows 10" on Windows 10:

https://www.microsoft.com/en-au/download/details.aspx?id=45520




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
