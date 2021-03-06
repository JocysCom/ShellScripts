using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

public class IsPortOpen
{

	public static int ProcessArguments(string[] args)
	{
		Console.Title = string.Format("Testing Ports from {0} computer.", Environment.MachineName);
		var ic = new InstallContext(null, args);
		var taskFile = (ic.Parameters["TaskFile"] ?? "").Replace("\"", "");
		var computer = (ic.Parameters["Computer"] ?? "").Replace("\"", "");
		var protocol = (ic.Parameters["Protocol"] ?? "").Replace("\"", "");
		var pSourceAddress = (ic.Parameters["SA"] ?? "").Replace("\"", "");
		var pSourcePort = (ic.Parameters["SP"] ?? "").Replace("\"", "");
		var pDestinationAddress = (ic.Parameters["DA"] ?? "").Replace("\"", "");
		var pDestinationPort = (ic.Parameters["DP"] ?? "").Replace("\"", "");
		// Set default protocol.
		if (string.IsNullOrEmpty(protocol))
			protocol = "TCP";
		// Get ports
		int sourcePort;
		int destinationPort;
		int.TryParse(pSourcePort, out sourcePort);
		int.TryParse(pDestinationPort, out destinationPort);
		var tasks = new PortTasks();
		FileInfo fi = null;
		// Save task.
		if (string.IsNullOrEmpty(taskFile))
		{
			// Get local configurations.
			var keys = "1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ";
			var files = System.IO.Directory.GetFiles(".", "*.xml")
				.OrderBy(x => x)
				.Take(keys.Length)
				.Select(x => new FileInfo(x))
				.ToList();
			if (files.Count > 0)
			{
				Console.WriteLine("Select configuration file:");
				Console.WriteLine("");
				for (int i = 0; i < files.Count; i++)
				{
					var file = files[i];
					var name = Path.GetFileNameWithoutExtension(file.Name);
					Console.WriteLine("    {0} - {1}", keys[i], name);

				}
				Console.WriteLine();
				Console.Write("Type Number or press ENTER to exit: ");
				var key = Console.ReadKey(true);
				Console.WriteLine(key.KeyChar);
				Console.WriteLine();
				var keyIndex = keys.IndexOf(key.KeyChar);
				if (keyIndex  > -1)
					taskFile = files[keyIndex].Name;
			}
		}
		if (!string.IsNullOrEmpty(taskFile))
		{
			fi = new FileInfo(taskFile);
		}
		Console.Title += string.Format(" Configuration: {0}", taskFile);
		if (fi != null && fi.Exists)
		{
			tasks = Deserialize<PortTasks>(taskFile);
		}
		else
		{
			var task = new PortTask();
			task.Computer = computer;
			task.Protocol = protocol;
			task.SourceAddress = pSourceAddress;
			task.SourcePort = sourcePort;
			task.DestinationAddress = pDestinationAddress;
			task.DestinationPort = destinationPort;
			task.ErrorCode = 0;
			task.ErrorMessage = "";
			tasks.Items.Add(task);
			Serialize(tasks, taskFile);
		}
		var sa = "Source Address";
		var mn = Environment.MachineName;
		// Get maximum computer length.
		var maxCHost = tasks.Items.Max(x => (x.Computer ?? "").Length);
		maxCHost = Math.Max(maxCHost, "Computer".Length);
		maxCHost = Math.Max(maxCHost, mn.Length);
		// Get maximum source host length.
		var maxSHost = tasks.Items.Max(x => (x.SourceAddress ?? "").Length);
		maxSHost = Math.Max(maxSHost, sa.Length);
		// Get destination source host length.
		var maxDHost = tasks.Items.Max(x => (x.DestinationAddress ?? "").Length);
		maxDHost = Math.Max(maxDHost, "Destination Host".Length);
		// Get description length.	
		var maxDesc = tasks.Items.Max(x => (x.Description ?? "").Length);
		maxDesc = Math.Max(maxDesc, "Description".Length);
		var format1 = "{0,4} {1,-" + maxCHost + "} {2,-4} {3,-" + maxSHost + "} {4,5} {5,-" + maxDHost + "} {6,5}";
		var formatO = " {0,-5}";
		var formatD = " {0,-" + maxDesc + "}";
		Console.Write(format1, "Test", "Computer", "Type", sa, "Port", "Destination Host", "Port");
		Console.Write(formatO, "Open");
		Console.WriteLine(formatD, "Description");
		// Add header separator.
		var s04 = new string('-', 4);
		var s05 = new string('-', 5);
		var sCH = new string('-', maxCHost);
		var sSH = new string('-', maxSHost);
		var sDH = new string('-', maxDHost);
		var sD = new string('-', maxDesc);
		Console.Write(format1, s04, sCH, s04, sSH, s05, sDH, s05);
		Console.Write(formatO, s05);
		Console.WriteLine(formatD, sD);
		// Add lines.
		for (int i = 0; i < tasks.Items.Count; i++)
		{
			var task = tasks.Items[i];
			var isOpen = false;
			if (task.Protocol == "ICMP")
			{
				Exception error;
				isOpen = _Ping(task.DestinationAddress, 5000, out error);
			}else
			{
				isOpen = _IsPortOpen(task.DestinationAddress, task.DestinationPort, 20000, 1, false, task.SourceAddress, task.SourcePort);
			}
			var comp = task.Computer;
			if (string.IsNullOrEmpty(comp))
				comp = mn;
			var sp = task.SourcePort > 0 ? task.SourcePort.ToString() : "any";
			var dp = task.DestinationPort > 0 ? task.DestinationPort.ToString() : "";
			if (task.Protocol == "ICMP"){
				sp = "";
				dp = "";
			}
			Console.Write(format1, i + 1, mn, task.Protocol, task.SourceAddress, sp, task.DestinationAddress, dp);
			// Write result.
			var org = Console.ForegroundColor;
			Console.ForegroundColor = isOpen ? ConsoleColor.Green : ConsoleColor.Red;
			Console.Write(formatO, isOpen);
			Console.ForegroundColor = org;
			// Add description.
			Console.WriteLine(formatD, task.Description);
		}
		return 0;
	}

	public static bool _IsPortOpen(string host, int port, int timeout = 20000, int retry = 1, bool isUdp = false, string localAddress = null, int localPort = 0)
	{
		IPEndPoint localEndPoint = null;
		if (!string.IsNullOrEmpty(localAddress))
			localEndPoint = new IPEndPoint(IPAddress.Parse(localAddress), localPort);
		var retryCount = 0;
		while (retryCount < retry)
		{
			// Logical delay without blocking the current thread.
			if (retryCount > 0)
				Task.Delay(timeout).Wait();
			if (isUdp)
			{
				// UDP is connectionless by design, therefore method below is not 100 % reliable.
				// Firewall and network setups might influence the result.
				var udp = localEndPoint == null
					? new UdpClient()
					: new UdpClient(localEndPoint);
				try
				{
					var result = udp.BeginSend(new byte[1], 1, host, port, null, null);
					var success = result.AsyncWaitHandle.WaitOne(timeout);
					if (success)
					{
						udp.BeginReceive(null, null);
						udp.EndSend(result);

						var result2 = udp.BeginReceive(null, null);
						var success2 = result.AsyncWaitHandle.WaitOne(timeout);
						if (success2)
						{
							IPEndPoint remoteEP = null;
							udp.EndReceive(result2, ref remoteEP);
							return true;
						}
					}
				}
				catch (SocketException udpEx)
				{
					// If port was forcibly closed (WSAECONNRESET) then...
					if (udpEx.ErrorCode == 10054)
						return false;
				}
				finally
				{
					udp.Close();
					retryCount++;
				}
				// Answer or timeout means that port is open.
				return true;
			}
			else
			{
				var tcp = localEndPoint == null
					? new TcpClient()
					: new TcpClient(localEndPoint);
				try
				{
					var result = tcp.BeginConnect(host, port, null, null);
					var success = result.AsyncWaitHandle.WaitOne(timeout);
					if (success)
					{
						tcp.EndConnect(result);
						return true;
					}
				}
				catch { }
				finally
				{
					tcp.Close();
					retryCount++;
				}
			}

		}
		return false;
	}

	static bool _Ping(string hostNameOrAddress, int timeout, out Exception error)
	{
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
		return success;
	}


	static T Deserialize<T>(string path)
	{
		if (!File.Exists(path))
			return default(T);
		var xml = File.ReadAllText(path);
		var sr = new StringReader(xml);
		var settings = new XmlReaderSettings();
		settings.DtdProcessing = DtdProcessing.Ignore;
		settings.XmlResolver = null;
		var reader = XmlReader.Create(sr, settings);
		var serializer = new XmlSerializer(typeof(T), new Type[] { typeof(T) });
		var o = (T)serializer.Deserialize(reader);
		reader.Dispose();
		return o;
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

}

[XmlRoot("PortTasks")]
public class PortTasks
{
	public PortTasks() { Items = new List<PortTask>(); }
	[XmlElement("PortTask")]
	public List<PortTask> Items { get; set; }
}

public class PortTask
{
	/// <summary>Computer to run test on.</summary>
	[XmlAttribute] public string Computer { get; set; }
	[XmlAttribute] public string Protocol { get; set; }
	[XmlAttribute] public string DestinationAddress { get; set; }
	[XmlAttribute] public int DestinationPort { get; set; }
	[XmlAttribute] public string SourceAddress { get; set; }
	[XmlAttribute] public int SourcePort { get; set; }
	[XmlAttribute] public int ErrorCode { get; set; }
	[XmlAttribute] public string ErrorMessage { get; set; }
	[XmlAttribute] public string Description { get; set; }
}