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

	public static void ProcessArguments(string[] args)
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
		maxs.Add(list.Max(x => string.Format("{0:yyyy-MM-dd HH:mm}", x.LastLogon).Length));
		maxs.Add(list.Max(x => string.Format("{0}", x.Os).Length));
		maxs.Add(list.Max(x => string.Format("{0}", x.OsPack).Length));
		maxs.Add(list.Max(x => string.Format("{0}", x.OsVersion).Length));
		// Flush servers.
		var servers = list.Where(x => x.Os.Contains("Server")).ToArray();
		Write(servers, "Domain_Servers", true, maxs);
		Write(servers, "Domain_Servers", false, maxs);
		list = list.Except(servers).ToArray();
		var clients = list.Where(x => x.Os.Contains("Windows")).ToArray();
		Write(clients, "Domain_Clients", true, maxs);
		Write(clients, "Domain_Clients", false, maxs);
		list = list.Except(clients).ToArray();
		Write(list, "Domain_Other", true, maxs);
		Write(list, "Domain_Other", false, maxs);
		Console.WriteLine();
	}

	static void Write(Computer[] list, string file, bool active, List<int> maxs)
	{
		var sb = new StringBuilder();
		var now = DateTime.Now;
		// Get list of computers which connected in last 5 weeks.
		var activeList = list.Where(x => x.LastLogon.HasValue && now.Subtract(x.LastLogon.Value) < new TimeSpan(7 * 5, 0, 0, 0, 0)).ToArray();
		var absentList = list.Except(activeList).ToArray();
		list = active ? activeList : absentList;
		for (int i = 0; i < list.Length; i++)
		{
			var item = list[i];
			var m = 0;
			sb.AppendFormat("{0,-" + maxs[m++] + "}  {1,-" + maxs[m++] + "}  {2,-" + maxs[m++] + ":yyyy-MM-dd HH:mm}  {3,-" + maxs[m++] + "}  {4,-" + maxs[m++] + "}  {5,-" + maxs[m++] + "}\r\n",
				item.Name, item.Address, item.LastLogon, item.Os, item.OsPack, item.OsVersion
			);
			//sb.AppendFormat("{0,{1},{2:yyyy-MM-dd HH:mm},{3},{4},{5}\r\n",
			//	item.Name, item.Address, item.LastLogon, item.Os, item.OsPack, item.OsVersion
			//);
		}
		System.IO.File.WriteAllText(file + (active ? "_active" : "_absent") + ".txt", sb.ToString());
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

