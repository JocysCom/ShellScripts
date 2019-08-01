using System;
using System.Configuration.Install;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

public class Test_SSL_Support
{

	public static int ProcessArguments(string[] args)
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
			//ServicePointManager.ServerCertificateValidationCallback = ValidateServerCertificate;
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
					true, ValidateServerCertificate);
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
		return 0;
	}

	/*

	:: Additionally path to certificates must be added to prevent broken chain issues.
	SET cer=-CApath ".\certs"
	SET dom=domain.com
	:: HTTPS
	openssl s_client -connect %dom%:443 %cer%
	:: FTPS
	openssl s_client -connect %dom%:990 %cer%
	:: FTP (STARTTLS)
	openssl s_client -connect %dom%:21 -starttls ftp %cer%
	:: SMTPS:
	openssl s_client -connect smtp.gmail.com:465 %cer%
	:: SMTP (STARTTLS)
	openssl s_client -connect smtp.gmail.com:587 -starttls smtp %cer%
	:: POP3
	openssl s_client -connect pop.gmail.com:995 %cer%
	:: POP3 (STARTTLS)
	openssl s_client -connect pop.gmail.com:25 -starttls pop3 %cer%
	:: IMAP
	openssl s_client -connect imap.gmail.com:993 %cer%
	:: IMAP (STARTTLS)
	openssl s_client -connect imap.gmail.com:143 -starttls imap %cer%

	*/

	/// <summary>
	/// </summary>
	static void TestSMTP()
	{
		const string server = "smtp.gmail.com";
		const int port = 587;
		using (var client = new TcpClient(server, port))
		{
			using (var stream = client.GetStream())
			using (var clearTextReader = new StreamReader(stream))
			using (var clearTextWriter = new StreamWriter(stream) { AutoFlush = true })
			using (var sslStream = new SslStream(stream))
			{
				// Sending EHLO instead of HELO will normally get a response with multiple lines,
				// showing all commands supported by the server, each on its own line starting with 250.
				// If you want to use EHLO, you will need to loop, calling clearTextReader.ReadLine() 
				// until the last line of the response starts with 250 (including the space after the response code).
				// SMTP responses with more lines to follow in the response start with 250-,
				// while the last line starts with 250 (including the space). 
				var connectResponse = clearTextReader.ReadLine();
				if (!connectResponse.StartsWith("220"))
					throw new InvalidOperationException("SMTP Server did not respond to connection request");
				clearTextWriter.WriteLine("HELO");
				var helloResponse = clearTextReader.ReadLine();
				if (!helloResponse.StartsWith("250"))
					throw new InvalidOperationException("SMTP Server did not respond to HELO request");
				// STARTTLS
				clearTextWriter.WriteLine("STARTTLS");
				var startTlsResponse = clearTextReader.ReadLine();
				if (!startTlsResponse.StartsWith("220"))
					throw new InvalidOperationException("SMTP Server did not respond to STARTTLS request");
				sslStream.AuthenticateAsClient(server);
				using (var reader = new StreamReader(sslStream))
				using (var writer = new StreamWriter(sslStream) { AutoFlush = true })
				{
					writer.WriteLine("EHLO " + server);
					Console.WriteLine(reader.ReadLine());
				}
			}
		}
		Console.WriteLine("Press Enter to exit...");
		Console.ReadLine();
	}

	#region Ignore invalid SSL Certificate

	/// <summary>
	/// The following method is invoked by the RemoteCertificateValidationDelegate.
	/// Net.ServicePointManager.ServerCertificateValidationCallback = ValidateServerCertificate
	/// </summary>
	/// <remarks>
	/// Add "AllowCertificateErrors" to allow certificate errors: request.Headers.Add("AllowCertificateErrors");
	/// One line example of allowing all invalid certificates.
	/// (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => { return true; }
	/// </remarks>
	public static bool ValidateServerCertificate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
	{
		// No errors were found.
		if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
		{
			// Allow this client to communicate with unauthenticated servers.
			return true;
		}
		var allow = true;
		string message = string.Format("Certificate error: {0}", sslPolicyErrors);
		if (allow)
		{
			message += " Allow this client to communicate with unauthenticated server.";
		}
		else
		{
			message += " The underlying connection was closed.";
		}
		var ex = new Exception("Validate server certificate error");
		ex.Data.Add("AllowCertificateErrors", allow);
		if (sender != null && sender is System.Net.HttpWebRequest)
		{
			//var request = (System.Net.HttpWebRequest)sender;
			// Allow certificate errors if request contains "AllowCertificateErrors" key.
			//AllowCertificateErrors = request.Headers.AllKeys.Contains("AllowCertificateErrors");
			var hr = (System.Net.HttpWebRequest)sender;
			ex.Data.Add("sender.OriginalString", hr.Address.OriginalString);
		}
		if (certificate != null)
		{
			ex.Data.Add("Certificate.Issuer", certificate.Issuer);
			ex.Data.Add("Certificate.Subject", certificate.Subject);
		}
		if (chain != null)
		{
			for (int i = 0; i < chain.ChainStatus.Length; i++)
			{
				ex.Data.Add("Chain.ChainStatus(" + i + ")", string.Format("{0}, {1}", chain.ChainStatus[i].Status, chain.ChainStatus[i].StatusInformation));
			}
		}
		Console.WriteLine(ex.Message);
		foreach (var key in ex.Data.Keys)
		{
			Console.WriteLine("    {0}: {1}", key, ex.Data[key]);
		}
		// Allow (or not allow depending on setting value) this client to communicate with unauthenticated servers.
		return allow;
	}


	#endregion


}

