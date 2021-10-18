<#
.SYNOPSIS
    Convert Folder with SVG image files into XAML Resource file.
.NOTES
    Author:     Evaldas Jocys <evaldas@jocys.com>
    Modified:   2021-10-11
.LINK
    http://www.jocys.com

.REMARKS

    Requires Installation of InkScape from https://inkscape.org/release/

	How to include icons resource into App.xaml file:

		<Application
			x:Class="JocysCom.SomeApp"
			xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			StartupUri="MainWindow.xaml">
			<Application.Resources>
				<ResourceDictionary>
					<ResourceDictionary.MergedDictionaries>
						<ResourceDictionary Source="Resources/Icons/Icons_Default.xaml" />
					</ResourceDictionary.MergedDictionaries>
				</ResourceDictionary>
			</Application.Resources>
		</Application>
	
	How to display image inside the XAML with style:
	
		<ContentControl	x:Name="MyIcon" Width="24" Height="24" Content="{StaticResource Icon_IconFileName}" />	

	How to set image to Content control from code behind:

		MyIcon.Content = Icons_Default.Current[Icons_Default.Icon_IconFileName];
		
#>
using namespace System;
using namespace System.IO;
using namespace System.Xml.Linq;
using namespace System.Text.RegularExpressions;

[Reflection.Assembly]::LoadWithPartialName("System.Xml.Linq") | Out-Null;

Clear-Host;

# ----------------------------------------------------------------------------
# Get current command path.
[string]$current = $MyInvocation.MyCommand.Path;
# Get calling command path.
[string]$calling = @(Get-PSCallStack)[1].InvocationInfo.MyCommand.Path;
# If executed directly then...
if ($calling -ne "") {
    $current = $calling;
}
# ----------------------------------------------------------------------------
[FileInfo]$file = New-Object FileInfo($current);
# Set public parameters.
$global:scriptName = $file.Basename;
$global:scriptPath = $file.Directory.FullName;
# Change current directory.
[Console]::WriteLine("Script Path: {0}", $scriptPath);
[Environment]::CurrentDirectory = $scriptPath;
Set-Location $scriptPath;
# ----------------------------------------------------------------------------
[DirectoryInfo]$root = New-Object DirectoryInfo($scriptPath);
# ----------------------------------------------------------------------------
function RemoveAttributes
{
    param([XElement]$Node,[string]$Name);
    #------------------------------------------
    foreach ($attr in $Node.Attributes())
    {
        if ($attr.Name -eq $Name)
        {
            $attr.Remove();
        }
    }
    foreach ($child in $Node.Descendants())
    {
        RemoveAttributes -Node $child -Name $Name;
    }
}
# ----------------------------------------------------------------------------
function FindParentFile
{
    [OutputType([FileInfo[]])] param([string]$pattern);
    #------------------------------------------
    [DirectoryInfo]$di = new-Object DirectoryInfo $scriptPath;
    do
    {
        $files = $di.GetFiles($pattern);
        # Return if project file was found.
        if ($files.Count -gt 0)
        {
            return $files;
        }
        # Continue to parent.
        $di = $di.Parent;
    } while($null -ne $di);
    return $null;
}
# ----------------------------------------------------------------------------
function GetProjectValue
{
    [OutputType([string])] param([string]$path, [string]$name);
    #------------------------------------------
    [string]$content = [File]::ReadAllText($path);
	[Regex]$rx = New-Object Regex("(?<p><$name>)(?<v>[^<]*)(?<s><\/$name>)");
	$match = $rx.Match($content);
	if ($match.Success -eq $true) {
		return $match.Groups["v"].Value;
	}
	return $null;
}
# ----------------------------------------------------------------------------
function GetExePath
{
    [OutputType([string])] param([string]$path);
    #------------------------------------------
    # Paths to lookf for executable.
    $ps = @(
        $path,
        "${env:ProgramFiles}\$path",
        "${env:ProgramFiles(x86)}\$path",
        "D:\Program Files\$path",
        "D:\Program Files (x86)\$dfe"
    );
    foreach ($p in $ps) {
        if ([File]::Exists($p)) {
            $path = $p;
            break;
        }
    }
    # Fix dot notations.
    $combined = [Path]::GetFullPath($path);
    return $combined;
}
# ----------------------------------------------------------------------------
function SHA256CheckSum
{
    param($filePath);
    $SHA256 = [System.Security.Cryptography.SHA256Managed]::Create();
    $fileStream = [System.IO.File]::OpenRead($filePath);
    $bytes = $SHA256.ComputeHash($fileStream);
    $hash = ($bytes|ForEach-Object ToString X2) -join '';
    $fileStream.Dispose();
    $SHA256.Dispose();
    return $hash;
}
# ----------------------------------------------------------------------------

Write-Host;

# Create regular expressions for key and names generation.
$RxAllExceptNumbersAndLetters = New-Object Regex("[^a-zA-Z0-9]", [RegexOptions]::IgnoreCase);
$UsRx = New-Object Regex("_+");
# Inkscape program location, which will be used for conversion from SVG format to XAML format.
$inkscape = GetExePath "Inkscape\bin\inkscape.exe";
if ("" -eq "$inkscape") {
    Write-Host "Inkscape program not found!";
    Write-Host "Download from https://inkscape.org/release/";
    return;
}
# Get Project file.
[string]$projectFilePath = $null;
[string]$projectFileBaseName = $null;
$projects = FindParentFile "*.*proj";
if ($projects.Count -gt 0){
    $projectFilePath = $projects[0].FullName;
    $projectFileBaseName = $projects[0].BaseName;
}
if ("" -eq "$projectFilePath") {
    Write-Host "Project file not found.";
    return;
}
Write-Host "Project:  $projectFilePath";
# Get namespace from project file.
$namespace = GetProjectValue $projectFilePath "RootNamespace";
# If default namespace not found.
if ("" -eq "$namespace") {
    # Visual studio use Project file name as default assembly and root namespace.
    $namespace = $projectFileBaseName;
}

if ("" -eq "$namespace") {
	Write-Host "Please provice namespace";
    $namespace = Host-Read;
}
# Get Class Name
$className = $file.Name.Split(".")[0];
$dir = New-Object DirectoryInfo($root.FullName + "\" + $className);

Write-Host "Class:    $namespace.$className";
Write-Host "Source:   $($dir.Name)\";
Write-Host "Target:   $className.xaml + $className.xaml.cs";
Write-Host;

#Write-Host "Done. Press any key to continue...";
#$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown");

# Get files.
$files = $dir.GetFiles("*.svg");
# If no SVG images found then skip.
if ($files.Length -eq 0){
    continue;
}
# Crate output file name.
$fileName = $RxAllExceptNumbersAndLetters.Replace($dir.Name, "_");
$fileName = $UsRx.Replace($fileName, "_");
$fileName = "$className.xaml";
$fileNameCs = "$className.xaml.cs";
if ($files.Length -eq 1){
    Write-Host "Convert  $($files.Length) image:";
}else{
    Write-Host "Convert $($files.Length) images:";
}
# Start <ResourceName>.xaml file.
$xNs = "http://schemas.microsoft.com/winfx/2006/xaml";
if ([File]::Exists($fileName) -ne $true)
{
    [File]::WriteAllText($fileName, "<ResourceDictionary xmlns=`"http://schemas.microsoft.com/winfx/2006/xaml/presentation`" xmlns:x=`"$xNs`"");
    [File]::AppendAllText($fileName,"`r`nx:Class=`"$($namespace).$($className)`"");
    [File]::AppendAllText($fileName,"`r`nx:ClassModifier=`"public`"");
    [File]::AppendAllText($fileName,'>');
    [File]::AppendAllText($fileName,"`r`n`r`n</ResourceDictionary>");
}
[XDocument]$xaml = [XDocument]::Load($fileName); 
$nodes = $xaml.Root.Nodes();
$xaml.Root.RemoveNodes();
# Start <ResourceName>.xaml.cs file.
[File]::WriteAllText($fileNameCs, "using System.Windows;`r`n");
[File]::AppendAllText($fileNameCs, "`r`n");
[File]::AppendAllText($fileNameCs, "namespace $namespace`r`n");
[File]::AppendAllText($fileNameCs, "{`r`n");
[File]::AppendAllText($fileNameCs, "`tpartial class $className : ResourceDictionary`r`n");
[File]::AppendAllText($fileNameCs, "`t{`r`n");
[File]::AppendAllText($fileNameCs, "`t`tpublic $className()`r`n");
[File]::AppendAllText($fileNameCs, "`t`t{`r`n");
[File]::AppendAllText($fileNameCs, "`t`t`tInitializeComponent();`r`n");
[File]::AppendAllText($fileNameCs, "`t`t}`r`n");
[File]::AppendAllText($fileNameCs, "`r`n");
[File]::AppendAllText($fileNameCs, "`t`tpublic static $className Current => _Current = _Current ?? new $className();`r`n");
[File]::AppendAllText($fileNameCs, "`t`tprivate static $className _Current;`r`n");
[File]::AppendAllText($fileNameCs, "`r`n");

Write-Host;

# Process files.
for ($f = 0; $f -lt $files.Length; $f++) {
    $file = $files[$f];
    #$hash = SHA256CheckSum -filePath $file.FullName;
    Write-Host "`t$($dir.Name)\$($file.Name)";
    $nodeXml = Get-Content "$($file.FullName)" | & $inkscape --pipe --export-type=xaml | Out-String;
    # Remove name attributes.
    [XDocument]$node = [XDocument]::Parse($nodeXml);
    # Remove "Name" attributes.
    RemoveAttributes -Node $node.Root -Name "Name";
    # Create unique key.
    $key = "Icon_$($file.BaseName)";
    # Add image XML to XAML document.
    $xaml.Root.Add($node.Root);
    # Get node which was just added.
    $ln = $xaml.Root.LastNode;
    # Give node unique name.
    $ln.SetAttributeValue([XName]::Get("Key", $xNs), $key);
    # Make sure that image copy is made when it is used.
    $ln.SetAttributeValue([XName]::Get("Shared", $xNs), "False");
    # Set file hash.
    $ln.SetAttributeValue([XName]::Get("FileHash", $xNs), $hash);
    # Write unique name to code file.
    [File]::AppendAllText($fileNameCs, "`t`tpublic const string $key = nameof($key);`r`n");
}
# Save XAML file.
$xaml.Save($fileName);
# End <ResourceName>.xaml.cs file.
[File]::AppendAllText($fileNameCs, "`r`n");
[File]::AppendAllText($fileNameCs, "`t}`r`n");
[File]::AppendAllText($fileNameCs, "}`r`n");

Write-Host;
Write-Host "Done. Press any key to continue...";
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown");
