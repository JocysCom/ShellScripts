using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.DirectoryServices;
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
			.OrderBy(x=>x.Sytem).ThenBy(x=>x.Name).ToArray();
		var sb = new StringBuilder();
		// Flush servers.
		var servers = list.Where(x => x.Sytem.Contains("Server")).ToArray();
		for (int i = 0; i < servers.Length; i++)
		{
			var item = servers[i];
			sb.AppendFormat("{0},{1},{2},{3},{4}\r\n", item.Name, item.Address, item.Sytem, item.Pack, item.Version);
		}
		System.IO.File.WriteAllText("Domain_Servers.csv", sb.ToString());
		// Flush Clients.
		sb.Clear();
		list = list.Except(servers).ToArray();
		var clients = list.Where(x => x.Sytem.Contains("Windows")).ToArray();
		for (int i = 0; i < clients.Length; i++)
		{
			var item = clients[i];
			sb.AppendFormat("{0},{1},{2},{3},{4}\r\n", item.Name, item.Address, item.Sytem, item.Pack, item.Version);
		}
		System.IO.File.WriteAllText("Domain_Clients.csv", sb.ToString());
		list = list.Except(clients).ToArray();
		// Flush other.
		sb.Clear();
		for (int i = 0; i < list.Length; i++)
		{
			var item = list[i];
			sb.AppendFormat("{0},{1},{2},{3},{4}\r\n", item.Name, item.Address, item.Sytem, item.Pack, item.Version);
		}
		System.IO.File.WriteAllText("Domain_Other.csv", sb.ToString());
		Console.WriteLine("{0} names exported", list.Length);
		Console.WriteLine();
	}

	public static List<Computer> GetComputers(string domain)
	{
		var list = new List<Computer>();
		var entry = new DirectoryEntry("LDAP://" + domain);
		var searcher = new DirectorySearcher(entry);
		searcher.Filter = ("(objectClass=computer)");
		searcher.SizeLimit = int.MaxValue;
		searcher.PageSize = int.MaxValue;
		var all = searcher.FindAll().Cast<SearchResult>().ToArray();
		Console.Write("Progress: ");
		for (int i = 0; i < all.Length; i++)
		{
			var result = all[i];
			var item = result.GetDirectoryEntry();
			var name = item.Name;
			var os = string.Format("{0}", item.Properties["OperatingSystem"].Value)
				.Replace("Standard", "")
				.Replace("Datacenter", "")
				.Replace("Enterprise", "")
				.Replace("®", "")
				.Replace("™", "")
				.Trim();
			var sp = os.Contains("Windows")
				? string.Format("{0}", item.Properties["OperatingSystemServicePack"].Value)
				.Replace("Service Pack ", "SP")
				.Trim()
				: "";
			var ov = string.Format("{0}", item.Properties["OperatingSystemVersion"].Value).Trim();
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
			};
			list.Add(computer);
			Write(i, all.Length);
		}
		Console.WriteLine();
		searcher.Dispose();
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

