using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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
		var list = GetComputers(domain)
			.OrderBy(x => x.Os).ThenBy(x => x.Name).ToArray();
		Console.WriteLine("{0} names exported", list.Length);
		//item.Name, item.Address, item.LastLogon, item.Os, item.OsPack, item.OsVersion
		var maxs = new List<int>();
		maxs.Add(list.Max(x => string.Format("{0}", x.Name).Length));
		maxs.Add(list.Max(x => string.Format("{0}", x.Address).Length));
		maxs.Add(list.Max(x => string.Format("{0}", x.OpenPort).Length));
		maxs.Add(list.Max(x => string.Format("{0:yyyy-MM-dd HH:mm}", x.LastLogon).Length));
		maxs.Add(list.Max(x => string.Format("{0}", x.Os).Length));
		maxs.Add(list.Max(x => string.Format("{0}", x.OsPack).Length));
		maxs.Add(list.Max(x => string.Format("{0}", x.OsVersion).Length));
		// Flush servers.
		var servers = list.Where(x => x.Os.Contains("Server")).ToArray();
		Write(servers, domain + "_Servers", true, maxs);
		Write(servers, domain + "_Servers", false, maxs);
		list = list.Except(servers).ToArray();
		var clients = list.Where(x => x.Os.Contains("Windows")).ToArray();
		Write(clients, domain + "_Clients", true, maxs);
		Write(clients, domain + "_Clients", false, maxs);
		list = list.Except(clients).ToArray();
		Write(list, domain + "_Other", true, maxs);
		Write(list, domain + "_Other", false, maxs);
		Console.WriteLine();
		return 0;
	}

	static bool outputTypeIsCsv = true;

	static void Write(Computer[] list, string file, bool active, List<int> maxs)
	{
		var sb = new StringBuilder();
		var now = DateTime.Now;
		// Get list of computers which connected in last 5 weeks.
		var activeList = list.Where(x => x.LastLogon.HasValue && now.Subtract(x.LastLogon.Value) < new TimeSpan(7 * 5, 0, 0, 0, 0)).ToArray();
		var absentList = list.Except(activeList).ToArray();
		list = active ? activeList : absentList;
		UpdateIsOnline(list);
		for (int i = 0; i < list.Length; i++)
		{
			var item = list[i];
			var m = 0;
			var format = outputTypeIsCsv
				? "{0},{1},{2},{3:yyyy-MM-dd HH:mm},{4},{5},{6}\r\n"
				: "{0,-" + maxs[m++] + "}  {1,-" + maxs[m++] + "} {2,-" + maxs[m++] + "}  {3,-" + maxs[m++] + ":yyyy-MM-dd HH:mm}  {4,-" + maxs[m++] + "}  {5,-" + maxs[m++] + "}  {6,-" + maxs[m++] + "}\r\n";
			sb.AppendFormat(format, item.Name, item.Address, item.OpenPort, item.LastLogon, item.Os, item.OsPack, item.OsVersion);
		}
		System.IO.File.WriteAllText(file + (active ? "_active" : "_absent") + (outputTypeIsCsv ? ".csv" : ".txt"), sb.ToString());
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
				.Replace("®", "")
				.Replace("™", "")
				.Trim();
			var sp = os.Contains("Windows")
				? string.Format("{0}", sr.Properties["OperatingSystemServicePack"].Value)
				.Replace("Service Pack ", "SP")
				.Trim()
				: "";
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
				var addresses = ipaddress.Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).FirstOrDefault();
				ips = string.Join(" ", addresses);
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
				LastLogon = ll,
				OpenPort = 0,
			};
			list.Add(computer);
			Write(i, all.Length);
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
						computer.LastLogon = auth.LastLogon;
						computer.SamAccountName = auth.SamAccountName;
						computer.UserPrincipalName = auth.UserPrincipalName;

					}
				}
			}
		}
		Console.WriteLine();
		ds.Dispose();
		entry.Dispose();
		return list;
	}

	public class Computer
	{
		public string Name;
		public string Os;
		public string OsVersion;
		public string OsPack;
		public string Address;
		public string SamAccountName;
		public string UserPrincipalName;
		public DateTime? LastLogon;
		public int OpenPort;
	}


	public static void Write(int i, int max)
	{
		var l = max.ToString().Length;
		var s = string.Format("{0," + l + "}/{1}", i + 1, max);
		Console.CursorVisible = i + 1 == max;
		if (i > 0)
			for (var c = 0; c < s.Length; c++)
				Console.Write("\b");
		Console.Write(s);
	}

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

	static void UpdateIsOnline(Computer computer)
	{
		try
		{
			// NetBIOS UDP 137.
			CheckNetBios(computer);
			// RPC TCP 135.
			if (computer.OpenPort == 0 && IsPortOpen(computer.Name, 135))
				computer.OpenPort = 135;
			// RDP TCP 3389.
			if (computer.OpenPort == 0 && IsPortOpen(computer.Name, 3389))
				computer.OpenPort = 3389;
			Console.WriteLine("{0,-16} Port: {1,4}", computer.Name, computer.OpenPort);
		}
		catch (Exception ex)
		{
			Console.WriteLine("{0} Exception: {1}", computer.Name, ex.Message);
		}
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
	static void CheckNetBios(Computer computer)
	{
		var receiveBuffer = new byte[1024];
		var requestSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		requestSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 2000);
		var addressList = Dns.GetHostAddresses(computer.Name);
		if (addressList.Length == 0)
		{
			//Console.WriteLine("NetBIOS: {0} host could not be found.", computer.Name);
			return;
		}
		EndPoint remoteEndpoint = new IPEndPoint(addressList[0], 137);
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
				computer.OpenPort = 137;
				//Console.WriteLine("NetBIOS: {0} is online.", deviceName);
			}
		}
		catch (SocketException)
		{
			//Console.WriteLine("NetBIOS: {0} could not be identified.", computer.Name);
		}
	}

	public static void UpdateIsOnline(Computer[] computers)
	{
		Parallel.ForEach(computers,
		new ParallelOptions { MaxDegreeOfParallelism = 16 },
		   x => UpdateIsOnline(x)
		);
	}

	#endregion

}

