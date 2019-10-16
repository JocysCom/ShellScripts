using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

public class List_Domain_Computers
{

	public static int ProcessArguments(string[] args)
	{
		//for (int i = 0; i < args.Length; i++)
		//	Console.WriteLine(string.Format("{0}. {1}", i, args[i]));
		// Requires System.Configuration.Installl reference.
		var ic = new InstallContext(null, args);
		var domain = ic.Parameters["domain"];
		if (string.IsNullOrEmpty(domain))
			domain = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName;
		Console.WriteLine("Domain: {0}", domain);
		Console.WriteLine("Folder: {0}", Environment.CurrentDirectory);
		//GetVirtualMachines(domain);
		var list = GetComputers(domain);
		Console.WriteLine("{0} names exported", list.Count);
		// Filter by type.
		Console.WriteLine();
		Console.WriteLine("Type:");
		Console.WriteLine("");
		Console.WriteLine("    0 - All");
		Console.WriteLine("    1 - Servers");
		Console.WriteLine("    2 - Clients");
		Console.WriteLine();
		Console.Write("Type Number or press ENTER to exit: ");
		var key = Console.ReadKey(true);
		Console.WriteLine(string.Format("{0}", key.KeyChar));
		var suffix = "_domain_computers";
		if (key.KeyChar == '1')
		{
			list = list.Where(x => x.Type == "Server").ToList();
			suffix = "_domain_servers";
		}
		else if (key.KeyChar == '2')
		{
			list = list.Where(x => x.Type == "Client").ToList();
			suffix = "_domain_clients";
		}
		else if (key.KeyChar != '0')
		{
			return 0;
		}
		list = list.OrderByDescending(x => x.Type).ThenBy(x => x.OS).ThenBy(x => x.Name).ToList();
		Write(list, domain + suffix);
		Console.WriteLine();
		return 0;
	}

	static void Write(List<Computer> list, string file, bool? active = null)
	{
		var sb = new StringBuilder();
		var now = DateTime.Now;
		var suffix = "";
		if (active.HasValue)
			suffix = active.Value ? "_active" : "_passive";
		var fileName = file + suffix + ".xls";
		Console.WriteLine("File Name: {0}", fileName);
		ParallelAction(list, UpdateIsOnline, "NET");
		var activeList = list.Where(x => !string.IsNullOrEmpty(x.OpenPort)).ToList();
		if (active.HasValue)
		{
			var absentList = list.Except(activeList).ToList();
			list = active.Value ? activeList : absentList;
		}
		// Reduce threads or requests could be blocked.
		ParallelAction(activeList, FillMacAddress, "MAC", 2);
		ParallelAction(activeList, FillSqlVersion, "SQL", 4);
		Console.WriteLine("{0}: Write", fileName);
		var table = new Table();
		table.Rows = list;
		Serialize(table, fileName);
	}

	public static List<string> GetVirtualMachines(string domain)
	{
		var list = new List<string>();
		var entry = new DirectoryEntry("LDAP://" + domain);
		var ds = new DirectorySearcher(entry);
		ds.Filter = "(&(objectClass=serviceConnectionPoint)(CN=Windows Virtual Machine))";
		ds.SizeLimit = int.MaxValue;
		ds.PageSize = int.MaxValue;
		var all = ds.FindAll().Cast<SearchResult>().ToArray();
		Console.Write("Progress: ");
		for (int i = 0; i < all.Length; i++)
		{
			var result = all[i];
			var sr = result.GetDirectoryEntry();
			var name = sr.Name;
			var cn = string.Format("{0}", sr.Properties["CanonicalName"].Value);
			var dn = string.Format("{0}", sr.Properties["DistinguishedName"].Value);

			Console.WriteLine("{0} - {1} - {2}", dn, name, cn);
			list.Add(cn);
		}
		return list;
	}

	public static List<Computer> GetComputers(string domain)
	{
		var list = new List<Computer>();
		var entry = new DirectoryEntry("LDAP://" + domain);
		var ds = new DirectorySearcher(entry);
		//ds.PropertiesToLoad.AddRange(new string[] { "samAccountName", "lastLogon" });
		ds.Filter = ("(objectClass=computer)");
		ds.SizeLimit = int.MaxValue;
		ds.PageSize = int.MaxValue;
		var all = ds.FindAll().Cast<SearchResult>().ToArray();
		Console.Write("Progress: ");
		for (int i = 0; i < all.Length; i++)
		{
			var result = all[i];
			var sr = result.GetDirectoryEntry();
			var name = sr.Name;
			var os = string.Format("{0}", sr.Properties["OperatingSystem"].Value)
				.Replace("Standard", "")
				.Replace("Datacenter", "")
				.Replace("Enterprise", "")
				.Replace("Professional", "")
				.Replace("Business", "")
				.Replace("PC Edition", "")
				.Replace("Pro", "")
				.Replace("for Workstations", "")
				.Replace("®", "")
				.Replace("™", "")
				.Replace("Evaluation", "")
				.Trim();
			var sp = os.Contains("Windows")
				? string.Format("{0}", sr.Properties["OperatingSystemServicePack"].Value)
				.Replace("Service Pack ", "SP")
				.Trim()
				: "";
			os = os.Replace("2000 Server", "Server 2000").Trim();
			var ov = string.Format("{0}", sr.Properties["OperatingSystemVersion"].Value).Trim();
			DateTime? ll = null;
			//if (sr.Properties["LastLogonTimeStamp"] != null && sr.Properties["LastLogonTimeStamp"].Count > 0)
			//{
			//	long lastLogon = (long)sr.Properties["LastLogonTimeStamp"][0];
			//	ll = DateTime.FromFileTime(lastLogon);
			//}
			if (name.StartsWith("CN="))
				name = name.Remove(0, "CN=".Length);
			string ips = "";
			var host = string.Format("{0}.{1}", name, domain);
			try
			{
				var ipaddress = Dns.GetHostAddresses(host);
				var address = ipaddress
					.Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
					// Exclude The 169 IP range of addresses is reserved by Microsoft for private network addressing
					.Where(x => x.GetAddressBytes()[0] != 169)
					.OrderBy(x => x.ToString())
					.ToArray()
					.FirstOrDefault();
				ips = string.Join(" ", address);
			}
			catch (Exception)
			{
				ips = "Unknown";
				//throw;
				continue;
			}
			// Apply types.
			var computer = new Computer()
			{
				Name = name,
				OS = os.Contains("Server") ? os.Replace("Windows", "").Trim() : os,
				Version = ov,
				SP = sp,
				Address = ips,
				LastLogon = ll,
				OpenPort = null,
			};
			if (os.Contains("Windows"))
				computer.Type = os.Contains("Server") ? "Server" : "Client";
			list.Add(computer);
			ProgressWrite(i, all.Length);
		}
		using (var context = new PrincipalContext(ContextType.Domain, domain))
		{
			using (var searcher = new PrincipalSearcher(new ComputerPrincipal(context)))
			{
				foreach (var result in searcher.FindAll())
				{
					var auth = result as AuthenticablePrincipal;
					if (auth != null)
					{
						var computer = list.FirstOrDefault(x => x.Name == auth.Name);
						if (computer == null)
							continue;
						if (auth.LastLogon.HasValue)
						{
							var dateTime = auth.LastLogon.Value;
							dateTime = new DateTime(
								dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerMinute),
								dateTime.Kind
							);
							computer.LastLogon = dateTime;
						}
						computer.SamAccountName = auth.SamAccountName;
						computer.UserPrincipalName = auth.UserPrincipalName;
						computer.Description = auth.Description;
					}
				}
			}
		}
		Console.WriteLine();
		ds.Dispose();
		entry.Dispose();
		return list;
	}


	public static List<string> GetSqlInstances(string machineName)
	{
		var list = new List<string>();
		var serverKeyName = @"SOFTWARE\Microsoft\Microsoft SQL Server";
		var type = Microsoft.Win32.RegistryHive.LocalMachine;
		var regKey = Microsoft.Win32.RegistryKey.OpenRemoteBaseKey(type, machineName);
		var serverKey = regKey.OpenSubKey(serverKeyName);
		if (serverKey != null)
		{
			// For 32 bit instances on a 64 bit OS:
			// SOFTWARE\Wow6432Node\Microsoft\Microsoft SQL Server\Instance Names\SQL	
			var instancesKeyName = @"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL";
			//Console.WriteLine(instancesKeyName);
			var instancesKey = regKey.OpenSubKey(instancesKeyName);
			if (instancesKey == null)
			{
				//Console.WriteLine("SQL Server instances not found on {0} server.", machineName);
			}
			else
			{
				foreach (var instanceValueName in instancesKey.GetValueNames())
				{
					var instanceValueData = instancesKey.GetValue(instanceValueName);
					//Console.WriteLine("{0}={1}", instanceValueName, instanceValueData);
					var setupKeyName = serverKey + "\\" + instanceValueData + "\\Setup";
					//Console.WriteLine("{0}", setupKeyName);
					var setupKey = serverKey.OpenSubKey(instanceValueData + "\\Setup");
					if (setupKey == null)
					{
						//Console.WriteLine("SQL Server instance setup key not found on {0} server.", machineName);
					}
					var version = (string)setupKey.GetValue("Version");
					var patchLevel = (string)setupKey.GetValue("PatchLevel");
					var edition = (string)setupKey.GetValue("Edition");
					var productCode = (string)setupKey.GetValue("ProductCode");
					var sp = (int)setupKey.GetValue("SP");
					var sps = sp > 0 ? string.Format("SP{0}", sp) : "";
					var uninstallKeyName = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + productCode;
					var uninstallKey = regKey.OpenSubKey(uninstallKeyName);
					var displayName = "SQL Server";
					if (uninstallKey == null)
					{
						//Console.WriteLine("SQL Server uninstall key not found");
					}
					else
					{
						displayName = (string)uninstallKey.GetValue("DisplayName");
						//Console.WriteLine("{0}", displayName);
						uninstallKey.Close();
					}
					if (version.StartsWith("15.")) displayName = "2019";
					if (version.StartsWith("14.")) displayName = "2017";
					if (version.StartsWith("13.")) displayName = "2016";
					if (version.StartsWith("12.")) displayName = "2014";
					if (version.StartsWith("11.")) displayName = "2012";
					if (version.StartsWith("10.5")) displayName = "2008 R2";
					if (version.StartsWith("10.4")) displayName = "2008";
					if (version.StartsWith("10.3")) displayName = "2008";
					if (version.StartsWith("10.2")) displayName = "2008";
					if (version.StartsWith("10.1")) displayName = "2008";
					if (version.StartsWith("10.0")) displayName = "2008";
					if (version.StartsWith("9.")) displayName = "2005";
					if (version.StartsWith("8.")) displayName = "2000";
					// Write the version and edition info to output file
					var info = displayName + " " + sps + " " + patchLevel;
					if (!list.Contains(info))
						list.Add(info);
					//Console.WriteLine("{0}: {1} {2}", instanceValueData, info, edition);
					setupKey.Close();
				}
				instancesKey.Close();
			}
			serverKey.Close();
		}
		return list;
	}

	///// <summary>
	///// Works only for same subnet.
	///// </summary>
	///// <param name="remoteAddress"></param>
	///// <param name="sourceAddress"></param>
	///// <returns></returns>
	//public static PhysicalAddress GetMacAddress(IPAddress remoteAddress, IPAddress sourceAddress = null)
	//{
	//	if (remoteAddress.AddressFamily != AddressFamily.InterNetwork)
	//		throw new Exception("Only IP4 is supported");
	//	var remoteAddressInt = BitConverter.ToInt32(remoteAddress.GetAddressBytes(), 0);
	//	var sourceAddressInt = BitConverter.ToInt32((sourceAddress ?? IPAddress.Any).GetAddressBytes(), 0);
	//	var macAddress = new byte[6];
	//	var macAddrLen = macAddress.Length;
	//	var ret = NativeMethods.SendArp(remoteAddressInt, sourceAddressInt, macAddress, ref macAddrLen);
	//	if (ret != 0)
	//	{
	//		throw new System.ComponentModel.Win32Exception(ret);
	//	}
	//	return new PhysicalAddress(macAddress);
	//}

	public static PhysicalAddress GetMacAddress(string system, out Exception ex)
	{
		ex = null;
		// Try twice.
		for (int i = 0; i < 2; i++)
		{
			// Do not use the OS shell.
			var si = new System.Diagnostics.ProcessStartInfo();
			si.UseShellExecute = false;
			// Allow writing output to the standard output.
			si.RedirectStandardOutput = true;
			// Allow writing error to the standard error.
			si.RedirectStandardError = true;
			si.CreateNoWindow = true;
			si.FileName = "GETMAC";
			si.Arguments = string.Format("/S \"{0}\"", system);
			var output = new StringBuilder();
			var error = new StringBuilder();
			using (var outputWaitHandle = new AutoResetEvent(false))
			using (var errorWaitHandle = new AutoResetEvent(false))
			{
				var timeout = 8000;
				using (var p = new System.Diagnostics.Process() { StartInfo = si })
				{
					DataReceivedEventHandler outputReceived = (sender, e) =>
					{
						if (e.Data == null)
							outputWaitHandle.Set();
						else
							output.AppendLine(e.Data);
					};
					DataReceivedEventHandler errorReceived = (sender, e) =>
					{
						if (e.Data == null)
							errorWaitHandle.Set();
						else
							error.AppendLine(e.Data);
					};
					p.OutputDataReceived += outputReceived;
					p.ErrorDataReceived += errorReceived;
					p.Start();
					p.BeginErrorReadLine();
					p.BeginOutputReadLine();
					int exitCode;
					if (p.WaitForExit(timeout))
					{
						// Process completed. Check process.ExitCode here.
						exitCode = p.ExitCode;
					}
					else
					{
						// Timed out.
						ex = new Exception("Timeout");
					}
					// Detach events before disposing process.
					p.OutputDataReceived -= outputReceived;
					p.ErrorDataReceived -= errorReceived;
				}
				// Timeout handlers after 'p' is disposed to make sure that handers are not used in events.
				outputWaitHandle.WaitOne(timeout);
				errorWaitHandle.WaitOne(timeout);
			}
			// Process completed. Check process.ExitCode here.
			if (error.Length > 0)
				ex = new Exception(error.ToString().Trim('\r', '\n', ' '));
			// pattern to get all connections
			var rx = new System.Text.RegularExpressions.Regex(
				@"\s+(?<mac>([0-9A-F]{2}[:-]){5}([0-9A-F]{2}))\s+",
				System.Text.RegularExpressions.RegexOptions.IgnoreCase);
			var match = rx.Match(output.ToString());
			if (match.Success)
			{
				var mac = match.Groups["mac"].Value.Replace(":", "-");
				var bytes = StringToByteArray(mac);
				var address = new PhysicalAddress(bytes);
				ex = null;
				return address;
			}
		}
		return null;
	}

	public static byte[] StringToByteArray(string hex)
	{
		if (hex.StartsWith("0x"))
			hex = hex.Substring(2);
		hex = hex.Replace(":", "").Replace("-", "");
		var chars = hex.Length;
		var bytes = new byte[chars / 2];
		for (int i = 0; i < chars; i += 2)
			bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
		return bytes;
	}

	static class NativeMethods
	{
		/// <summary>
		/// Sends an ARP request to obtain the physical address that corresponds
		/// to the specified destination IP address.
		/// </summary>
		/// <param name="destIpAddress">Destination IP address, in the form of
		/// a <see cref="T:System.Int32"/>. The ARP request attempts to obtain
		/// the physical address that corresponds to this IP address.
		/// </param>
		/// <param name="srcIpAddress">IP address of the sender, in the form of
		/// a <see cref="T:System.Int32"/>. This parameter is optional. The caller
		/// may specify zero for the parameter.
		/// </param>
		/// <param name="macAddress">
		/// </param>
		/// <param name="macAddressLength">On input, specifies the maximum buffer
		/// size the user has set aside at pMacAddr to receive the MAC address,
		/// in bytes. On output, specifies the number of bytes written to
		/// pMacAddr.</param>
		/// <returns>If the function succeeds, the return value is NO_ERROR.
		/// If the function fails, use FormatMessage to obtain the message string
		/// for the returned error.
		/// </returns>
		[System.Runtime.InteropServices.DllImport("Iphlpapi.dll", EntryPoint = "SendARP")]
		internal extern static int SendArp(int destIpAddress, int srcIpAddress, byte[] macAddress, ref int macAddressLength);
	}

	public static void ProgressWrite(int i, int max)
	{
		var l = max.ToString().Length;
		var s = string.Format("{0," + l + "}/{1}", i + 1, max);
		Console.CursorVisible = i + 1 == max;
		if (i > 0)
			for (var c = 0; c < s.Length; c++)
				Console.Write("\b");
		Console.Write(s);
	}

	#region Ping

	public static bool Ping(string hostNameOrAddress, int timeout = 1000)
	{
		Exception error;
		return Ping(hostNameOrAddress, timeout, out error);
	}

	public static bool Ping(string hostNameOrAddress, int timeout, out Exception error)
	{
		var success = false;
		error = null;
		var sw = new System.Diagnostics.Stopwatch();
		sw.Start();
		System.Net.NetworkInformation.PingReply reply = null;
		Exception replyError = null;
		// Use proper threading, because other asynchronous classes
		// like "Tasks" have problems with Ping.
		var ts = new System.Threading.ThreadStart(delegate ()
		{
			var ping = new System.Net.NetworkInformation.Ping();
			try
			{
				reply = ping.Send(hostNameOrAddress);
			}
			catch (Exception ex)
			{
				replyError = ex;
			}
			ping.Dispose();
		});
		var t = new System.Threading.Thread(ts);
		t.Start();
		t.Join(timeout);
		if (reply != null)
		{
			success = (reply.Status == System.Net.NetworkInformation.IPStatus.Success);
		}
		else if (replyError != null)
		{
			error = replyError;
		}
		else
		{
			error = new Exception("Ping timed out (" + timeout.ToString() + "): " + sw.Elapsed.ToString());
		}
		return success;
	}

	#endregion

	#region Helper Methods

	public static bool IsPortOpen(string host, int port, int timeout = 2000, int retry = 1)
	{
		var retryCount = 0;
		while (retryCount < retry)
		{
			// Logical delay without blocking the current thread.
			if (retryCount > 0)
				System.Threading.Tasks.Task.Delay(timeout).Wait();
			var client = new System.Net.Sockets.TcpClient();
			try
			{
				var result = client.BeginConnect(host, port, null, null);
				var success = result.AsyncWaitHandle.WaitOne(timeout);
				if (success)
				{
					client.EndConnect(result);
					return true;
				}
			}
			catch
			{
				// ignored
			}
			finally
			{
				client.Close();
				retryCount++;
			}
		}
		return false;
	}

	// The following byte stream contains the necessary message
	// to request a NetBios name from a machine
	static byte[] NameRequest = new byte[]{
			0x80, 0x94, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x20, 0x43, 0x4b, 0x41,
			0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41,
			0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41,
			0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41,
			0x41, 0x41, 0x41, 0x41, 0x41, 0x00, 0x00, 0x21,
			0x00, 0x01 };


	/// <summary>
	/// Request NetBios name on UDP port 137. 
	/// </summary>
	/// <returns></returns>
	static bool CheckNetBios(Computer computer)
	{
		var receiveBuffer = new byte[1024];
		var requestSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		requestSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 2000);
		var ipaddress = Dns.GetHostAddresses(computer.Name);
		var addresses = ipaddress
			.Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
			// Exclude The 169 IP range of addresses is reserved by Microsoft for private network addressing
			.Where(x => x.GetAddressBytes()[0] != 169)
			.OrderBy(x => x.ToString())
			.ToArray();
		if (addresses.Length == 0)
		{
			//Console.WriteLine("NetBIOS: {0} host could not be found.", computer.Name);
			return false;
		}
		EndPoint remoteEndpoint = new IPEndPoint(addresses[0], 137);
		var originEndpoint = new IPEndPoint(IPAddress.Any, 0);
		requestSocket.Bind(originEndpoint);
		requestSocket.SendTo(NameRequest, remoteEndpoint);
		try
		{
			var receivedByteCount = requestSocket.ReceiveFrom(receiveBuffer, ref remoteEndpoint);
			if (receivedByteCount >= 90)
			{
				var enc = new ASCIIEncoding();
				var deviceName = enc.GetString(receiveBuffer, 57, 16).Trim();
				var networkName = enc.GetString(receiveBuffer, 75, 16).Trim();
				return true;
				//Console.WriteLine("NetBIOS: {0} is online.", deviceName);
			}
		}
		catch (SocketException)
		{
			//Console.WriteLine("NetBIOS: {0} could not be identified.", computer.Name);
		}
		return false;
	}

	static int ParallelCount;
	static int ParallelTotal;
	static object ParallelReportLock = new object();
	static string ParalelLineFormat;

	static void ParallelReport(string format, params object[] args)
	{
		var value = string.Format(format, args);
		Console.Write(value);
	}

	public static void ParallelAction(List<Computer> settingsList, Func<Computer, string> action, string group, int parallelTasks = 16)
	{
		ParallelCount = 0;
		ParallelTotal = settingsList.Count;
		var maxName = settingsList.Max(x => x.Name.Length);
		ParalelLineFormat = "{0} {1,5:0.0}% - {2," + ParallelTotal.ToString().Length + "}. {3,-" + maxName + "} - {4}\r\n";
		Parallel.ForEach(settingsList,
		new ParallelOptions { MaxDegreeOfParallelism = parallelTasks },
		   x => ParallelItemAction(x, action, group)
		);
	}

	public static void ParallelItemAction(Computer computer, Func<Computer, string> action, string group)
	{
		string result;
		try
		{
			result = string.Format("{0}", action(computer));
		}
		catch (Exception ex)
		{
			result = string.Format("Exception: {0}", ex.Message);
		}
		// Report.
		lock (ParallelReportLock)
		{
			System.Threading.Interlocked.Increment(ref ParallelCount);
			var percent = (decimal)ParallelCount / (decimal)ParallelTotal * 100m;
			ParallelReport(ParalelLineFormat, group, percent, ParallelCount, computer.Name, result);
		}
	}

	static string UpdateIsOnline(Computer computer)
	{
		// Try to PING first because it won't use and lock local port.
		if (string.IsNullOrEmpty(computer.OpenPort) && Ping(computer.Name, 2000))
			computer.OpenPort = "ICMP";
		// NetBIOS UDP 137.
		if (string.IsNullOrEmpty(computer.OpenPort) && CheckNetBios(computer))
			computer.OpenPort = "UDP/137";
		// RPC TCP 135.
		if (string.IsNullOrEmpty(computer.OpenPort) && IsPortOpen(computer.Name, 135))
			computer.OpenPort = "TCP/135";
		// RDP TCP 3389.
		if (string.IsNullOrEmpty(computer.OpenPort) && IsPortOpen(computer.Name, 3389))
			computer.OpenPort = "TCP/3389";
		return computer.OpenPort;
	}

	static string FillMacAddress(Computer computer)
	{
		string result;
		// Fill MAC address.
		var mac = "";
		try
		{
			Exception ex1;
			var pa = GetMacAddress(computer.Address, out ex1);
			if (ex1 != null)
			{
				result = string.Format("IP: {0,-15}, Exception: {1}", computer.Address, ex1.Message);
				return result;
			}
			if (pa != null)
				mac = BitConverter.ToString(pa.GetAddressBytes());
			result = string.Format("IP: {0,-15}, MAC: {1}", computer.Address, mac);
		}
		catch (Exception ex)
		{
			result = string.Format("IP: {0,-15}, Exception: {1}", computer.Address, ex.Message);
		}
		computer.MAC = mac;
		return result;
	}

	static string FillSqlVersion(Computer computer)
	{
		computer.SQL = string.Join(";", GetSqlInstances(computer.Address));
		return computer.SQL;
	}

	#endregion

	#region Serialize

	[XmlRoot("table")]
	public class Table
	{
		[XmlElement("row")]
		public List<Computer> Rows { get; set; }
	}

	public class Computer
	{
		public string Type { get; set; }
		// Address before name for better compatibility with IP-Name format.
		public string Address { get; set; }
		public string MAC { get; set; }
		public string Name { get; set; }
		public string OS { get; set; }
		public string Version { get; set; }
		public string SP { get; set; }
		[XmlIgnore] public string SamAccountName { get; set; }
		[XmlIgnore] public string UserPrincipalName { get; set; }
		public DateTime? LastLogon { get; set; }
		public string OpenPort { get; set; }
		public string SQL { get; set; }
		public string Description { get; set; }
	}

	static void Serialize<T>(T o, string path)
	{
		var settings = new XmlWriterSettings();
		//settings.OmitXmlDeclaration = true;
		settings.Encoding = System.Text.Encoding.UTF8;
		settings.Indent = true;
		settings.IndentChars = "\t";
		var serializer = new XmlSerializer(typeof(T));
		// Serialize in memory first, so file will be locked for shorter times.
		var ms = new MemoryStream();
		var xw = XmlWriter.Create(ms, settings);
		serializer.Serialize(xw, o);
		File.WriteAllBytes(path, ms.ToArray());
	}

	#endregion

}
