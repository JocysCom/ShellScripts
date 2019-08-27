using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.DirectoryServices;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

public class Test_Domain_TLS_Support
{

	public static int ProcessArguments(string[] args)
	{
		//for (int i = 0; i < args.Length; i++)
		//	Console.WriteLine(string.Format("{0}. {1}", i, args[i]));
		// Requires System.Configuration.Installl reference.
		var ic = new InstallContext(null, args);
		var domain = ic.Parameters["domain"];
		var computers = ic.Parameters["computers"];
		if (string.IsNullOrEmpty(domain))
			domain = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName;
		Console.WriteLine("Domain: {0}", domain);
		Console.WriteLine("Computers: {0}", computers);
		Console.WriteLine("Folder: {0}", Environment.CurrentDirectory);
		//GetVirtualMachines(domain);
		var computerList = computers.Split(',', ';').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToArray();
		var list = GetComputers(domain, computerList);
		Console.WriteLine("{0} names exported", list.Count);
		// Apply types.
		list.Where(x => x.Os.Contains("Windows")).ToList().ForEach(x => x.Type = "Client");
		list.Where(x => x.Os.Contains("Server")).ToList().ForEach(x => x.Type = "Server");
		// by default do not filter by server or client.
		var key = '0';
		if (computerList.Length == 0)
		{
			// Filter by type.
			Console.WriteLine();
			Console.WriteLine("Type:");
			Console.WriteLine("");
			Console.WriteLine("    0 - All");
			Console.WriteLine("    1 - Servers");
			Console.WriteLine("    2 - Clients");
			Console.WriteLine();
			Console.Write("Type Number or press ENTER to exit: ");
			key = Console.ReadKey(true).KeyChar;
			Console.WriteLine(string.Format("{0}", key));
			Console.WriteLine();
		}
		var suffix = "_domain_computers";
		if (key == '1')
		{
			list = list.Where(x => x.Type == "Server").ToList();
			suffix = "_domain_servers";
		}
		else if (key == '2')
		{
			list = list.Where(x => x.Type == "Client").ToList();
			suffix = "_domain_clients";
		}
		else if (key != '0')
		{
			return 0;
		}
		list = list.OrderBy(x => x.Name).ToList();
		// Determine which computers are online firsts.
		UpdateIsOnline(list);
		var onlineList = list.Where(x => !string.IsNullOrEmpty(x.OpenPort)).ToList();
		// Gather TLS data from registry.
		FillTlsData(onlineList);
		//Console.WriteLine("{0}: Write", fileName);
		var table = new Table();
		table.Rows = onlineList;
		Serialize(table, "TLS" + suffix + ".xml");
		Console.WriteLine();
		return 0;
	}

	static void FillTlsData(List<Computer> list)
	{
		var dict = new Dictionary<string, string>();
		dict.Add("MPTUH", @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\Multi-Protocol Unified Hello");
		dict.Add("PCT10", @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\PCT 1.0");
		dict.Add("SSL20", @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\SSL 2.0");
		dict.Add("SSL30", @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\SSL 3.0");
		dict.Add("TLS10", @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.0");
		dict.Add("TLS11", @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1");
		dict.Add("TLS12", @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2");
		var subKeys = new string[] { "Client", "Server" };
		for (int i = 0; i < list.Count; i++)
		{
			var item = list[i];
			Console.Write("TLS: {0,16}... ", item.Name);
			foreach (var key in dict.Keys)
			{
				var path = dict[key];
				// Store Enabled and DisabledByDefault values.
				var el = new List<int?>();
				var dl = new List<int?>();
				foreach (var subKey in subKeys)
				{
					var ts = new System.Threading.ThreadStart(delegate ()
					{
						try
						{
							var hklm = Microsoft.Win32.RegistryKey.OpenRemoteBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, item.Name);
							var regKey = hklm.OpenSubKey(path + "\\" + subKey);
							if (regKey != null)
							{
								var e = (int?)regKey.GetValue("Enabled", null);
								var d = (int?)regKey.GetValue("DisabledByDefault", null);
								el.Add(e);
								dl.Add(d);
								regKey.Close();
							}
							hklm.Close();
						}
						catch (Exception ex)
						{
							item.Error = ex.Message;
						}
					});
					var t = new System.Threading.Thread(ts);
					t.Start();
					t.Join(4000);
				}
				// Properly enabled if Enabled = 1 and DisabledByDefault = 0;
				var isEnabled = el.Count(x => x != null && x != 0) == 2 && dl.Count(x => x == 0) == 2;
				var isDisabled = el.Count(x => x == 0) == 2 && dl.Count(x => x != null && x != 0) == 2;
				switch (key)
				{
					case "MPTUH": item.MPTUH = isEnabled ? "+" : (isDisabled ? "-" : ""); break;
					case "PCT10": item.PCT10 = isEnabled ? "+" : (isDisabled ? "-" : ""); break;
					case "SSL20": item.SSL20 = isEnabled ? "+" : (isDisabled ? "-" : ""); break;
					case "SSL30": item.SSL30 = isEnabled ? "+" : (isDisabled ? "-" : ""); break;
					case "TLS10": item.TLS10 = isEnabled ? "+" : (isDisabled ? "-" : ""); break;
					case "TLS11": item.TLS11 = isEnabled ? "+" : (isDisabled ? "-" : ""); break;
					case "TLS12": item.TLS12 = isEnabled ? "+" : (isDisabled ? "-" : ""); break;
					default:
						break;
				}
			}
			if (string.IsNullOrEmpty(item.Error))
			{
				Console.WriteLine("MPTUH={0,1}, PCT10={1,1}, SSL20={2,1}, SSL30={3,1}, TLS10={4,1}, TLS11={5,1}, TLS12={6,1}",
					item.MPTUH, item.PCT10, item.SSL20, item.SSL30, item.TLS10, item.TLS11, item.TLS12);
			}
			else
			{
				Console.WriteLine(item.Error);
			}
		}
	}

	public static List<Computer> GetComputers(string domain, params string[] computers)
	{
		var list = new List<Computer>();
		var entry = new DirectoryEntry("LDAP://" + domain);
		var ds = new DirectorySearcher(entry);
		ds.Filter = "(objectClass=computer)";
		ds.SizeLimit = int.MaxValue;
		ds.PageSize = int.MaxValue;
		var all = ds.FindAll().Cast<SearchResult>().ToArray();
		Console.Write("Progress: ");
		for (int i = 0; i < all.Length; i++)
		{
			var result = all[i];
			var sr = result.GetDirectoryEntry();
			var name = sr.Name;
			if (name.StartsWith("CN="))
				name = name.Remove(0, "CN=".Length);
			// If computer filter specified and item is not in the list then continue
			if (computers.Length > 0 && !computers.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
				continue;
			var os = string.Format("{0}", sr.Properties["OperatingSystem"].Value)
			.Replace("Standard", "")
			.Replace("Datacenter", "")
			.Replace("Enterprise", "")
			.Replace("Professional", "")
			.Replace("Business", "")
			.Replace("PC Edition", "")
			.Replace("Pro", "")
			.Replace("®", "")
			.Replace("™", "")
			.Trim();
			var sp = os.Contains("Windows")
				? string.Format("{0}", sr.Properties["OperatingSystemServicePack"].Value)
				.Replace("Service Pack ", "SP")
				.Trim()
				: "";
			var ov = string.Format("{0}", sr.Properties["OperatingSystemVersion"].Value).Trim();
			string ips = "";
			var host = string.Format("{0}.{1}", name, domain);
			try
			{
				var ipaddress = Dns.GetHostAddresses(host);
				var addresses = ipaddress
					.Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
					// Exclude The 169 IP range of addresses is reserved by Microsoft for private network addressing
					.Where(x => x.GetAddressBytes()[0] != 169)
					.OrderBy(x => x.ToString())
					.ToArray();
				ips = string.Join(" ", addresses.FirstOrDefault());
			}
			catch (Exception)
			{
				ips = "Unknown";
				//throw;
				continue;
			}
			var computer = new Computer()
			{
				Name = name,
				Os = os,
				OsVersion = ov,
				OsPack = sp,
				Address = ips,
				OpenPort = null,
			};
			list.Add(computer);
			ProgressWrite(i, all.Length);
		}
		Console.WriteLine();
		ds.Dispose();
		entry.Dispose();
		return list;
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

	static int UpdateIsOnlineCount;
	static int UpdateIsOnlineTotal;

	public static void UpdateIsOnline(List<Computer> computers)
	{
		UpdateIsOnlineCount = 0;
		UpdateIsOnlineTotal = computers.Count;
		Parallel.ForEach(computers,
		new ParallelOptions { MaxDegreeOfParallelism = 16 },
		   x => UpdateIsOnline(x)
		);
	}

	static void UpdateIsOnline(Computer computer)
	{
		try
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
			// Report.
			System.Threading.Interlocked.Increment(ref UpdateIsOnlineCount);
			var percent = (decimal)UpdateIsOnlineCount / (decimal)UpdateIsOnlineTotal * 100m;
			Console.WriteLine("{0," + UpdateIsOnlineTotal.ToString().Length + "}. {1,-16} Port: {2,8} - {3,5:0.0}%",
				UpdateIsOnlineCount, computer.Name, computer.OpenPort, percent);
		}
		catch (Exception ex)
		{
			Console.WriteLine("{0} Exception: {1}", computer.Name, ex.Message);
		}
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
		public string Name { get; set; }
		public string Os { get; set; }
		public string OsVersion { get; set; }
		public string OsPack { get; set; }
		public string OpenPort { get; set; }
		public string Error { get; set; }
		// TLS Data.
		public string MPTUH { get; set; }
		public string PCT10 { get; set; }
		public string SSL20 { get; set; }
		public string SSL30 { get; set; }
		public string TLS10 { get; set; }
		public string TLS11 { get; set; }
		public string TLS12 { get; set; }
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
