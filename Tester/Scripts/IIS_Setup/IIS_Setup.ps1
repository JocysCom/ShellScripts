<#
.SYNOPSIS
    Import and Export IIS settings.
.NOTES
    Author:     Evaldas Jocys <evaldas@jocys.com>
    Modified:   2021-02-19

	Application Server Requirements (2012 R2):

	- To run applications compiled by Visual Studio 2019:
	  Microsoft .NET 5.0.1 Runtime - https://dotnet.microsoft.com/download 

	- To run PowerShell setup scripts:
	Windows Management Framework 5.1 - https://aka.ms/WMF5Download 
#>
#using assembly "System.Configuration";
#using assembly "System.Configuration.Install";
#using assembly "System.Xml";
using namespace System;
using namespace System.IO;
using namespace System.Linq;
using namespace System.Text;
using namespace System.Collections;
using namespace System.Collections.Generic;
using namespace System.Text.RegularExpressions;
using namespace System.Security.Cryptography;
using namespace System.Security.Cryptography.X509Certificates;

# ----------------------------------------------------------------------------
# Get current command path.
[string]$current = $MyInvocation.MyCommand.Path
# Get calling command path.
[string]$calling = @(Get-PSCallStack)[1].InvocationInfo.MyCommand.Path
# If executed directly then...
if ($calling -ne "") {
    $current = $calling
}
#----------------------------------------------------------------------------
# Run as administrator.
If (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator"))
{   
    $arguments = "& '" + $myinvocation.mycommand.definition + "'";
    Start-Process powershell -Verb runAs -ArgumentList $arguments;
    break;
}
# ----------------------------------------------------------------------------
import-module WebAdministration;
# ----------------------------------------------------------------------------
# Load code behind.
$code = [File]::ReadAllText("$current.cs");
Add-Type -TypeDefinition $code -Language CSharp -ReferencedAssemblies @("System.Xml");
# ----------------------------------------------------------------------------
function ConfigureSettings
{
    [FileInfo]$global:scriptFile = New-Object FileInfo($current);
    # Set public parameters.
	$global:scriptName = $scriptFile.Basename;
	$global:scriptPath = $scriptFile.Directory.FullName;
    # Cange current dir.
    [Environment]::CurrentDirectory = $scriptPath;
    # ----------------------------------------------------------------------------
    # Determine configuraiton file.
    Write-Host "Path: $scriptPath";
    Write-Host "Config: $configFile";
    ShowConfigurationMenu;
    #if ($configFile -eq $null -or [File]::Exists($configFile) -eq $false)
    #{
    #    [Console]::WriteLine("Configuration file not found!");
    #    pause;
    #    exit;
    #}
}
# ----------------------------------------------------------------------------
# Functions
# ----------------------------------------------------------------------------
function ImportPools
{
    [Data]$data = [IIS_Setup]::GetSettings($configFile);
    Write-Host "Import Pools...";
    Write-Host;
    for ($i = 0; $i -lt $data.Pools.Count; $i++) {
        $name = $data.Pools[$i].Name;
        Write-Host "  $name";
        $xmlFile = "$scriptPath\Pools\$name.xml";
        if ($null -eq $data.Config)
        {
            # IIS.
            Get-Content $xmlFile | & "${env:windir}\system32\inetsrv\appcmd.exe" add apppool /in
        }
        else
        {
            # IIS Express.
            Get-Content $xmlFile | & "${env:ProgramFiles(x86)}\IIS Express\appcmd.exe" add apppool /in /apphostconfig:"$($data.Config)"
        }
    }
    Write-Host;
}
# ----------------------------------------------------------------------------
function ImportSites
{
    [Data]$data = [IIS_Setup]::GetSettings($configFile);
    Write-Host "Import Sites...";
    Write-Host;
    for ($i = 0; $i -lt $data.Sites.Count; $i++) {
        $name = $data.Sites[$i].Name;
        Write-Host "  $name";
        $xmlFile = "$scriptPath\Sites\$name.xml";
        if ($null -eq $data.Config)
        {
            # IIS.
            Get-Content $xmlFile | & "${env:windir}\system32\inetsrv\appcmd.exe" add site /in
        }
        else
        {
            # IIS Express.
            Get-Content $xmlFile | & "${env:ProgramFiles(x86)}\IIS Express\appcmd.exe" add site /in /apphostconfig:"$($data.Config)"
        }
    }
    Write-Host;
}
# ----------------------------------------------------------------------------
function ExportPools
{
    [Data]$data = [IIS_Setup]::GetSettings($configFile);
    Write-Host "Export Pools...";
    Write-Host;
    for ($i = 0; $i -lt $data.Pools.Count; $i++) {
        $name = $data.Pools[$i].Name;
        Write-Host "  $name";
        if ([Directory]::Exists("$scriptPath\Pools") -eq $false)
        {
            [Directory]::CreateDirectory("$scriptPath\Pools");
        }
        $xmlFile = "$scriptPath\Pools\$name.xml";
        # Collect permissions and export to file.



        FormatXML $xmlFile;
    }
    Write-Host;
}
# ----------------------------------------------------------------------------
function ExportSites
{
    [Data]$data = [IIS_Setup]::GetSettings($configFile);
    Write-Host "Export Sites...";
    Write-Host;
    for ($i = 0; $i -lt $data.Sites.Count; $i++) {
        $name = $data.Sites[$i].Name;
        Write-Host "  $name";
        if ([Directory]::Exists("$scriptPath\Sites") -eq $false)
        {
            [Directory]::CreateDirectory("$scriptPath\Sites");
        }
        $xmlFile = "$scriptPath\Sites\$name.xml";
        if ($null -eq $data.Config)
        {
            # IIS.
            & "${env:windir}\system32\inetsrv\appcmd.exe" list site "$name" /config /xml > $xmlFile
        }
        else
        {
            # IIS Express.
            & "${env:ProgramFiles(x86)}\IIS Express\appcmd.exe" list site "$name" /config /xml /apphostconfig:"$($data.Config)" > $xmlFile
        }
        FormatXML $xmlFile;
    }
    Write-Host;
}
# ----------------------------------------------------------------------------
function ExportPaths
{
    [Data]$data = [IIS_Setup]::GetSettings($configFile);
    Write-Host "Export Paths...";
    Write-Host;
    for ($i = 0; $i -lt $data.Paths.Count; $i++) {
        $name = $data.Paths[$i].Name;
        Write-Host "  $name";
        if ([Directory]::Exists("$scriptPath\Paths") -eq $false)
        {
            [Directory]::CreateDirectory("$scriptPath\Paths");
        }
        $xmlFile = "$scriptPath\Paths\$name.xml";
        if ($null -eq $data.Config)
        {
            # IIS.
            & "${env:windir}\system32\inetsrv\appcmd.exe" list site "$name" /config /xml > $xmlFile
        }
        else
        {
            # IIS Express.
            & "${env:ProgramFiles(x86)}\IIS Express\appcmd.exe" list site "$name" /config /xml /apphostconfig:"$($data.Config)" > $xmlFile
        }
        FormatXML $xmlFile;
    }
    Write-Host;
}
# ----------------------------------------------------------------------------
function ListIIS
{
    param([string]$type);
    [Data]$data = [IIS_Setup]::GetSettings($configFile);
    if ($null -eq $data.Config)
    {
        & "${env:windir}\system32\inetsrv\appcmd.exe" LIST $type;
    }
    else
    {
        & "${env:ProgramFiles(x86)}\IIS Express\appcmd.exe" list $type /apphostconfig:"$($data.Config)";
    }
}
# ----------------------------------------------------------------------------
function CreateConfiguration
{
    Write-Host "Create Configuration...";
    [Data]$data = New-Object Data;
    # Get Pools.
    $pools = Get-ChildItem -Path IIS:\AppPools;
    $poolsExclude = [string[]](".NET v4.5", ".NET v4.5 Classic", "DefaultAppPool");
    Write-Host;
    Write-Host "Pools...";
    Write-Host;
    for ($i = 0; $i -lt $pools.Count; $i++) {
        $name = $pools[$i].Name;
        if ([Enumerable]::Contains($poolsExclude, $name)){
            continue;
        }
        Write-Host "  $name";
        [Item]$item = New-Object Item;
        $item.Name = $name;
        $data.Pools.Add($item);
    }
    # Get Sites.
    $sites = Get-ChildItem -Path IIS:\Sites;
    $sitesExclude = [string[]]("Default Web Site");
    Write-Host;
    Write-Host "Sites...";
    Write-Host;
    for ($i = 0; $i -lt $sites.Count; $i++) {
        $name = $sites[$i].Name;
        if ([Enumerable]::Contains($sitesExclude, $name)){
            continue;
        }
        Write-Host "  $name";
        [Item]$item = New-Object Item;
        $item.Name = $name;
        $data.Sites.Add($item);
        # Add path.
        [Item]$item = New-Object Item;
        $item.Name = $name;
        $item.Path = $sites[$i].PhysicalPath;
        $data.Paths.Add($item);
    }
    # Write configuration.
    Write-Host;
    $name = Read-Host -Prompt "Type configuration name and press ENTER to continue";
    Write-Host;
    [string]$global:configFile = "$scriptFile.$name.xml";
    # Data to XML file.
    [IIS_Setup]::Serialize([Data]$data, $configFile);
    [IIS_Setup]::SetSettings($configFile, $poolNames, $siteNames);
}
# ----------------------------------------------------------------------------
function FormatXML
{
    param([string]$FullName);
    $xml = [File]::ReadAllText($FullName);
    $xml = [IIS_Setup]::XmlFormat($xml);
    [void][File]::WriteAllText($FullName, $xml);
}
# ----------------------------------------------------------------------------
# Show menu
# ----------------------------------------------------------------------------
function ShowConfigurationMenu
{
    # Get local configurations.
	$keys = "1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    $files = [Directory]::GetFiles(".", "$scriptName*.xml");
    $files = [Enumerable]::OrderBy($files, [Func[object,bool]]{ param($x) $x });
    $files = [Enumerable]::Take($files, $keys.Length);
    $files = [Enumerable]::Select($files, [Func[object,object]]{ param($x) New-Object FileInfo $x });
    $files = [Enumerable]::ToList($files);
    if ($files.Count -eq 0)
    {
        $global:configFile = $null;
    }
    elseif ($files.Count -eq 1)
    {
        $global:configFile = $files[0].FullName;
    }
    elseif ($files.Count -gt 1)
    {
        Write-Host "Select configuration file:";
        Write-Host;
        for ($i = 0; $i -lt $files.Count; $i++)
        {
            $file = $files[$i];
            $name = [Path]::GetFileNameWithoutExtension($file.Name).Substring($scriptFile.Name.Length + 1);
            Write-Host "    $($keys[$i]) - $($name)";
        }
        Write-Host;
        $m = Read-Host -Prompt "Type option and press ENTER to continue";
        $keyIndex = $keys.IndexOf($m);
        # If wrong choice then...
        if ($keyIndex -eq -1)
        {
            $global:configFile = $files[$keyIndex].FullName;
            Write-Host "Config: $configFile";
            # Exit application.
            pause;
            exit;
        }
        else
        {
            $global:configFile = $files[$keyIndex].FullName;
        }
    }
}
# ----------------------------------------------------------------------------
function ShowImportExportMenu
{
    $m = "";
    do {
        # Clear screen.
        Clear-Host;
        Write-Host;
        if ($null -ne $configFile)
        {
            $name = [Path]::GetFileNameWithoutExtension($configFile).Substring($scriptFile.Name.Length + 1);
            Write-Host "Configuration: $name";
            Write-Host;
            Write-Host "  1 - List Pools";
            Write-Host "  2 - List Sites";
            Write-Host "  3 - List Applications";
            Write-Host "  4 - List Processes";
            Write-Host;
            Write-Host "  5 - Import IIS Pools from Files";
            Write-Host "  6 - Import IIS Sites from Files";
            Write-Host "  7 - Import IIS Paths from Files";
            Write-Host;
            Write-Host "  8 - Export IIS Pools to Files";
            Write-Host "  9 - Export IIS Sites to Files";
            Write-Host "  0 - Export IIS Paths to Files";
            Write-Host;
        }
        Write-Host "  C - Create Configuration";
        Write-Host;
        $m = Read-Host -Prompt "Type option and press ENTER to continue";
        Write-Host;
        # Options:
        IF ("${m}" -eq "1") { ListIIS APPPOOLS };
        IF ("${m}" -eq "2") { ListIIS SITE };
        IF ("${m}" -eq "3") { ListIIS APP };
        IF ("${m}" -eq "4") { ListIIS WP };
        # Import
        IF ("${m}" -eq "5") { ImportPools };
        IF ("${m}" -eq "6") { ImportSites };
        IF ("${m}" -eq "7") { ImportPaths };
        # Export.
        IF ("${m}" -eq "8") { ExportPools };
        IF ("${m}" -eq "9") { ExportSites };
        IF ("${m}" -eq "0") { ExportPaths };
        # Config.
        IF ("${m}" -eq "C") { CreateConfiguration; };
        # If option was choosen.
        IF ("${m}" -ne "") {
            pause;
        }
    } until ("${m}" -eq "");
    return $m;
}
# ----------------------------------------------------------------------------
# Execute.
# ----------------------------------------------------------------------------
# Configure settings.
ConfigureSettings
# Show certificate menu.
ShowImportExportMenu