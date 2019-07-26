using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;

public class Test_Domains
{

	public static int ProcessArguments(string[] args)
	{
		var reader = new System.IO.StreamReader("Test_Domains.list.csv");
		var lines = new List<string>();
		string line;
		while ((line = reader.ReadLine()) != null)
			lines.Add(line);
		var infos = new List<DomainInfo>();
		for (int i = 0; i < lines.Count; i++)
		{
			line = lines[i];
			var info = new DomainInfo();
			info.Name = line;
			info.WebName = "www." + line;
			Console.WriteLine(info.Name);
			//-------------------------------------------------
			info.Address = string.Join("|", CheckDns(info.Name));
			info.WebAddress = string.Join("|", CheckDns(info.WebName));
			info.Ping = Ping(info.Name);
			info.WebPing = Ping(info.WebName);
			info.HTTP = IsPortOpen(info.Name, 80);
			info.HTTPS = IsPortOpen(info.Name, 443);
			info.WebHTTP = IsPortOpen(info.WebName, 80);
			info.WebHTTPS = IsPortOpen(info.WebName, 443);
			//-------------------------------------------------
			infos.Add(info);

		}
		var results = new List<string>();
		results.Add(DomainInfo.ToHeader());
		results.AddRange(infos.Select(x => x.ToLine()));
		var contents = string.Join("\r\n", results);
		System.IO.File.WriteAllText("Test_Domains.results.csv", contents);
		return 0;
	}

	class DomainInfo
	{
		public string Name;
		public string Address;
		public bool Ping;
		public bool HTTP;
		public bool HTTPS;
		public string WebName;
		public string WebAddress;
		public bool WebPing;
		public bool WebHTTP;
		public bool WebHTTPS;

		static BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		public static string ToHeader()
		{
			var items = typeof(DomainInfo)
				.GetFields(flags)
				.Select(x => x.Name)
				.ToList();
			return string.Join(",", items);
		}

		public string ToLine()
		{
			var items = GetType()
				.GetFields(flags)
				.Select(x => x.GetValue(this))
				.Select(x=>string.Format("{0}", x))
				.ToList();
			return string.Join(",", items);
		}
	}

	#region Check DNS

	public static string[] CheckDns(string host)
	{
		// Test DNS - Try to resolve IP address.
		try
		{
			Console.Write("  Check DNS... ");
			var he = System.Net.Dns.GetHostEntry(host);
			var ips = he.AddressList.Select(x => string.Join(".", x.GetAddressBytes().Select(y => ((int)y).ToString()).ToArray())).ToArray();
			if (ips.Length == 0)
			{
				Console.WriteLine("Error: Host IP address is not available.");
			}
			else
			{
				Console.WriteLine(string.Join(", ", ips));
				return ips;
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error: " + ex.Message);
		}
		return new string[0];
	}

	#endregion

	#region Ping

	public static bool Ping(string hostNameOrAddress, int timeout = 1000)
	{
		Exception error;
		return Ping(hostNameOrAddress, timeout, out error);
	}

	public static bool Ping(string hostNameOrAddress, int timeout, out Exception error)
	{
		Console.Write("  Ping... ");
		var success = false;
		error = null;
		var sw = new System.Diagnostics.Stopwatch();
		sw.Start();
		PingReply reply = null;
		Exception replyError = null;
		// Use proper threading, because other asynchronous classes
		// like "Tasks" have problems with Ping.
		var ts = new System.Threading.ThreadStart(delegate ()
		{
			var ping = new Ping();
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
			success = (reply.Status == IPStatus.Success);
		}
		else if (replyError != null)
		{
			error = replyError;
		}
		else
		{
			error = new Exception("Ping timed out (" + timeout.ToString() + "): " + sw.Elapsed.ToString());
		}
		Console.WriteLine("{0} {1}", success, error);
		return success;
	}

	#endregion

	#region Port

	public static bool IsPortOpen(string host, int port, int timeout = 20000, int retry = 1)
	{
		var isOpen = false;
		var retryCount = 0;
		while (retryCount < retry)
		{
			// Logical delay without blocking the current thread.
			if (retryCount > 0)
				Task.Delay(timeout).Wait();
			var client = new TcpClient();
			try
			{
				var result = client.BeginConnect(host, port, null, null);
				var success = result.AsyncWaitHandle.WaitOne(timeout);
				if (success)
				{
					client.EndConnect(result);
					isOpen = true;
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
		Console.WriteLine("  Port: {0} - {1}", port, isOpen);
		return isOpen;
	}

	#endregion
}

