using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Net;
using System.Text;

public class List_Domain_Computers
{

	public static void Main(string[] args)
	{
		//for (int i = 0; i < args.Length; i++)
		//	Console.WriteLine(string.Format("{0}. {1}", i, args[i]));
		// Requires System.Configuration.Installl reference.
		var ic = new InstallContext(null, args);
		var domain = ic.Parameters["domain"];
		Console.WriteLine("Domain: {0}", domain);
		Console.WriteLine("Folder: {0}", Environment.CurrentDirectory);
		var list = GetComputers(domain)
			.OrderBy(x => x.Sytem).ThenBy(x => x.Name).ToArray();
		Console.WriteLine("{0} names exported", list.Length);
		// Flush servers.
		var servers = list.Where(x => x.Sytem.Contains("Server")).ToArray();
		Write(servers, "Domain_Servers.csv");
		list = list.Except(servers).ToArray();
		var clients = list.Where(x => x.Sytem.Contains("Windows")).ToArray();
		Write(clients, "Domain_Clients.csv");
		list = list.Except(clients).ToArray();
		Write(list, "Domain_Other.csv");
		Console.WriteLine();
	}

	static void Write(Computer[] list, string file)
	{
		var sb = new StringBuilder();
		for (int i = 0; i < list.Length; i++)
		{
			var item = list[i];
			sb.AppendFormat("{0},{1},{2:yyyy-MM HH:mm},{3},{4},{5}\r\n", item.Name, item.Address, item.Logon, item.Sytem, item.Pack, item.Version);
		}
		System.IO.File.WriteAllText(file, sb.ToString());
	}

	public static List<Computer> GetComputers(string domain)
	{
		var auths = new List<AuthenticablePrincipal>();

		using (var context = new PrincipalContext(ContextType.Domain, domain))
		{
			using (var searcher = new PrincipalSearcher(new ComputerPrincipal(context)))
			{
				foreach (var result in searcher.FindAll())
				{
					var auth = result as AuthenticablePrincipal;
					if (auth != null)
						auths.Add(auth);
				}
			}
		}

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
				Sytem = os,
				Version = ov,
				Pack = sp,
				Address = ips,
				Logon = ll,
			};
			list.Add(computer);
			Write(i, all.Length);
		}
		Console.WriteLine();
		ds.Dispose();
		entry.Dispose();
		return list;
	}

	public class Computer
	{
		public string Name;
		public string Sytem;
		public string Version;
		public string Pack;
		public string Address;
		public DateTime? Logon;
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

}

