<#
.SYNOPSIS
	Tests whether a TCP port on a host is reachable.
.DESCRIPTION
	Opens a TCP connection with a caller-supplied timeout and returns
	a deterministic exit code so tests can assert behaviour:
		0 = open, 1 = closed/timeout, 2 = error.
.EXAMPLE
	powershell -File IsPortOpen.ps1 -Computer 127.0.0.1 -Port 1 -TimeoutMs 200
#>
[CmdletBinding()]
param(
	[string]$Computer = "localhost",
	[int]$Port = 80,
	[int]$TimeoutMs = 500,
	[switch]$Quiet
)

$client = New-Object System.Net.Sockets.TcpClient
try {
	$task = $client.ConnectAsync($Computer, $Port)
	$completed = $task.Wait($TimeoutMs)
	if ($completed -and $client.Connected) {
		if (-not $Quiet) { Write-Output "OPEN ${Computer}:${Port}" }
		exit 0
	}
	if (-not $Quiet) { Write-Output "CLOSED ${Computer}:${Port}" }
	exit 1
}
catch {
	if (-not $Quiet) { Write-Output "ERROR $($_.Exception.Message)" }
	exit 2
}
finally {
	$client.Close()
}
