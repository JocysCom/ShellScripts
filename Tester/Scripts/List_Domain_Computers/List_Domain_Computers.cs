using System;
using System.Configuration.Install;
using System.DirectoryServices;
using System.Collections.Generic;

public class List_Domain_Computers
{

	public static void Main(string[] args)
	{
		//for (int i = 0; i < args.Length; i++)
		//	Console.WriteLine(string.Format("{0}. {1}", i, args[i]));
		// Requires System.Configuration.Installl reference.
		var ic = new InstallContext(null, args);
		var domain = ic.Parameters["domain"];
		var list = GetComputers(Environment.UserDomainName);
		var contents = string.Join("\r\n", list);
		System.IO.File.WriteAllText("Domain_Computers.csv", contents);
		Console.WriteLine();
	}

	
	public static List<string> GetComputers(string domain)
	{
		var list = new List<string>();
		var entry = new DirectoryEntry("LDAP://"+domain);
		var searcher = new DirectorySearcher(entry);
		searcher.Filter = ("(objectClass=computer)");
		searcher.SizeLimit = int.MaxValue;
		searcher.PageSize = int.MaxValue;
		foreach (SearchResult result in searcher.FindAll())
		{
			var item = result.GetDirectoryEntry();
			var name = item.Name;
			var os = string.Format("{0}", item.Properties["OperatingSystem"].Value);
			var sp = string.Format("{0}", item.Properties["OperatingSystemServicePack"].Value);
			var ov = string.Format("{0}", item.Properties["OperatingSystemVersion"].Value);
			if (name.StartsWith("CN="))
				name = name.Remove(0, "CN=".Length);
			list.Add(string.Format("{0}, {1}, {2}, {3}",name, os, sp, ov));
		}
		searcher.Dispose();
		entry.Dispose();
		return list;
	}

}

