# Gather TLS Information from all domain computers.

https://github.com/JocysCom/ShellScripts/tree/master/Tester/Scripts/Test_Domain_TLS_Support

You can drop **Test_Domain_TLS_Support.bat** and **Test_Domain_TLS_Support.cs** script files into any domain computer and run as Domain Administrator.

**Test_Domain_TLS_Support.cs** C# script will:

1. Gather computer names from domain controller.
2. Ask if you want to collect data for Servers, Clients or all computers.
3. Identify all computers which are online i.e. computer answers to ping, NetBIOS or other requests.
3. Collect information about security protocols from registry on these computers.
4. Create **TLS_domain_servers.xml** file, which can be opened with Microsoft Excel:

   Protocol status: [+] Enabled, [-] Disabled, [ ] Other State

![https://raw.githubusercontent.com/wiki/JocysCom/XmlTransform/Images/TlsSupportResults.png](https://raw.githubusercontent.com/wiki/JocysCom/XmlTransform/Images/TlsSupportResults.png) 
