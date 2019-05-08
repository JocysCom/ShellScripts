using System;
using System.Configuration.Install;
using System.Net;
using System.Linq;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

public class Program
{

	public static void Main(string[] args)
	{
		//for (int i = 0; i < args.Length; i++)
		//	Console.WriteLine(string.Format("{0}. {1}", i, args[i]));
		// Requires System.Configuration.Installl reference.
		var ic = new InstallContext(null, args);
		var host = ic.Parameters["host"];
		var port = int.Parse(ic.Parameters["port"] ?? "443");
		var protocols = ((SslProtocols[])Enum.GetValues(typeof(SslProtocols))).ToList();
		Console.Write("{0}:{1}\r\n\r\n", host, port);
		protocols.Remove(SslProtocols.Default);
		protocols.Remove(SslProtocols.None);
		for (int i = 0; i < protocols.Count; i++)
		{
			// Enable TLS 1.1, 1.2 and 1.3
			var Tls11 = 0x0300; //   768
			var Tls12 = 0x0C00; //  3072
			ServicePointManager.SecurityProtocol |= (SecurityProtocolType)(Tls11 | Tls12);
			var protocol = protocols[i];
			var client = new System.Net.Sockets.TcpClient();
			SslStream stream = null;
			bool status;
			string exchangeAlgorithm = null;
			string cipherAlgorithm = null;
			string hashAlgorithm = null;
			try
			{
				client.Connect(host, port);
				stream = new SslStream(client.GetStream(),
					true,
				   (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => { return true; });
				stream.ReadTimeout = 15000;
				stream.WriteTimeout = 15000;
				stream.AuthenticateAsClient(host, null, protocol, false);
				exchangeAlgorithm = string.Format("{0}", stream.KeyExchangeAlgorithm).ToUpper();
				if ((int)stream.KeyExchangeAlgorithm == 44550)
					exchangeAlgorithm = "ECDH";
				cipherAlgorithm = string.Format("{0}", stream.CipherAlgorithm).ToUpper();
				hashAlgorithm = string.Format("{0}", stream.HashAlgorithm).ToUpper();
				status = true;
			}
			catch
			{
				status = false;
			}
			Console.Write(
				"  {0,-5} = {1,-5}",
				protocol, status);
			var extra = status
				? string.Format(" | Exchange = {0,-5} | Cipher = {1,-5} | Hash = {2,-6}",
					exchangeAlgorithm, cipherAlgorithm, hashAlgorithm)
				: "";
			Console.WriteLine(extra);
			client.Close();
			if (stream != null)
				stream.Dispose();
		}
		Console.WriteLine();
	}


}

