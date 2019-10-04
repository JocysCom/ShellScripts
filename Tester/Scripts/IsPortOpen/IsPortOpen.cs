using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

public class IsPortOpen
{

	public static int ProcessArguments(string[] args)
	{
		//Console.WriteLine("Args: {0}", string.Join(", ", args));
		Console.Title = "Testing TCP Ports";
		Console.WriteLine("Testing TCP Port...");
		var destinationAddress = args[1];
		int destinationPort = int.Parse(args[2]);
		var sourceAddress = args[3];
		int sourceport;
		int.TryParse(args[4], out sourceport);
		var isOpen = _IsPortOpen(destinationAddress, destinationPort, 20000, 1, false, sourceAddress, sourceport);
		Console.WriteLine();
		Console.WriteLine("    Source Address: {0}", sourceAddress);
		Console.WriteLine("    Source Port: {0}", sourceport);
		Console.WriteLine("    Destination Address: {0}", destinationAddress);
		Console.WriteLine("    Destination Port: {0}", destinationPort);
		Console.Write("    Port is Open: ");
		var org = Console.ForegroundColor;
		Console.ForegroundColor = isOpen ? ConsoleColor.Green : ConsoleColor.Red;
		Console.Write("{0}", isOpen);
		Console.ForegroundColor = org;
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



}
