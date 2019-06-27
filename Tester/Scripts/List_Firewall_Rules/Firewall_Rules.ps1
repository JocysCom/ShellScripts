#-DisplayGroup 'Remote Desktop'
# -All
Get-NetFirewallRule -Direction 'Inbound' |
Select-Object -Skip 1 Direction,
@{Name='Protocol';Expression={($PSItem | Get-NetFirewallPortFilter).Protocol}},
@{Name='LocalPort';Expression={($PSItem | Get-NetFirewallPortFilter).LocalPort}},
@{Name='RemotePort';Expression={($PSItem | Get-NetFirewallPortFilter).RemotePort}},
@{Name='RemoteAddress';Expression={($PSItem | Get-NetFirewallAddressFilter).RemoteAddress}},
Enabled,
Profile,
Action,
DisplayGroup,
DisplayName | Where-Object {($_.Protocol -eq 'TCP') -or ($_.Protocol -eq 'UDP')} | Export-Csv –Path Firewall_Rules.csv
