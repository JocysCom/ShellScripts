using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

public class IsPortOpen
{

	public static int ProcessArguments(string[] args)
	{
		Console.Title = "Testing Ports";
		Console.WriteLine("Testing Port...");
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
		// Save task.
		if (!string.IsNullOrEmpty(taskFile))
		{
			var fi = new FileInfo(taskFile);
			if (!fi.Exists)
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
				tasks.Items.Add(task);
				Serialize(tasks, taskFile);

			}
		}
		else
		{
			tasks = Deserialize<PortTasks>(taskFile);
		}
		var format = "{0,4} {1,16} {2,4} {1,16} {1,5} {1,16} {1,5}";
		var result = " {0,5}";
		Console.WriteLine(format + result, "ID", "Computer", "Type", "Source Host", "SRC Port", "Destination Host", "DST Port", "Open");
		var s04 = "----";
		var s05 = "-----";
		var s16 = "----------------";
		Console.WriteLine(format + result, s04, s16, s04, s16, s05, s16, s05);
		for (int i = 0; i < tasks.Items.Count; i++)
		{
			var task = tasks.Items[i];
			var isOpen = _IsPortOpen(pDestinationAddress, destinationPort, 20000, 1, false, pSourceAddress, sourcePort);
			Console.WriteLine();
			Console.Write(format, i, computer, protocol, pSourceAddress, sourcePort, pDestinationAddress, destinationPort);
			var org = Console.ForegroundColor;
			Console.ForegroundColor = isOpen ? ConsoleColor.Green : ConsoleColor.Red;
			Console.WriteLine(result, isOpen);
			Console.ForegroundColor = org;
		}
		Console.WriteLine();
		Console.WriteLine();
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
}