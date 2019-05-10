Import-Module WebAdministration

foreach($WebSite in $(get-website))
{
   $logFile="$($Website.logFile.directory)\w3svc$($website.id)".replace("%SystemDrive%",$env:SystemDrive)
   Write-host "$logfile - $($WebSite.name)"
} 