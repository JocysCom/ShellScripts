<#
.SYNOPSIS
    Convert Folder with SVG image files into XAML Resource file.
.NOTES
    Author:     Evaldas Jocys <evaldas@jocys.com>
    Modified:   2021-10-11
.LINK
    http://www.jocys.com
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

$dir = New-Object DirectoryInfo($root.FullName + "\input");
$out = New-Object DirectoryInfo($root.FullName + "\output");

Write-Host "Source:   $($dir.Name)\";
Write-Host "Target:   $($out.Name)\";
Write-Host;

# Add PNG files.
$files = $dir.GetFiles("*.png");
$items = [System.Linq.Enumerable]::ToList($files);
# Add JPG files.
$files = $dir.GetFiles("*.jpg");
$items.AddRange($files);

for ($i = 0; $i -lt $items.Count; $i++) {
    $item = $items[$i];
    $percent = ($i / $items.Count) * 100;
    Write-Progress -Activity "Converting $($item.Name)" -PercentComplete $percent -Status "AVIF"
    # https://github.com/Kagami/go-avif/releases
    Invoke-Expression ".\bin\avif-win-x64.exe -q 20 -e `"$($item.FullName)`" -o `"$($out.FullName)\$($item.BaseName).avif`""
} 

Write-Progress -Activity "Finished!" -Status "DONE" -Completed
Write-Host;
Write-Host "Done. Press any key to continue...";
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown");