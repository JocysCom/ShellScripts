using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

public class Test_SSL_Support
{

	public class Result
	{
		public string Host;
		public int Port;
		public int Timeout;
		public SslProtocols Protocol;
		public bool Success;
		public string ExchangeAlgorithm;
		public string CipherAlgorithm;
		public string HashAlgorithm;
		public Exception CertificateError;

		public void UpdateFromCertificate(X509Certificate2 cert,
			SslPolicyErrors errors = SslPolicyErrors.None)
		{
			_Certificate = cert;
			_ExchangeKeyName = cert.PublicKey.EncodedKeyValue.Oid.FriendlyName;
			var rsa = cert.GetRSAPublicKey();
			var dsa = cert.GetDSAPublicKey();
			var ecc = cert.GetECDsaPublicKey();
			if (rsa != null)
				_ExchangeKeySize = rsa.KeySize;
			if (dsa != null)
				_ExchangeKeySize = dsa.KeySize;
			if (ecc != null)
				_ExchangeKeySize = ecc.KeySize;
			ExchangeAlgorithm = string.Format("{0}-{1}", _ExchangeKeyName, _ExchangeKeySize);
			_SslPolicyErrors = errors;
		}

		public void UpdateFromSslStream(SslStream stream)
		{
			CipherAlgorithm = string.Format("{0}", stream.CipherAlgorithm).ToUpper();
			HashAlgorithm = string.Format("{0}", stream.HashAlgorithm).ToUpper();
		}

		public X509Certificate2 Certificate { get { return _Certificate; } }
		private X509Certificate2 _Certificate;

		public SslPolicyErrors SslPolicyErrors { get { return _SslPolicyErrors; } }
		private SslPolicyErrors _SslPolicyErrors;

		public string ExchangeKeyName { get { return _ExchangeKeyName; } }
		private string _ExchangeKeyName;
		public int? ExchangeKeySize { get { return _ExchangeKeySize; } }
		private int? _ExchangeKeySize;

	}

	public static List<Result> Results = new List<Result>();
	private static Result result;

	#region IProgress

	public static Action<string> Progress;

	#endregion

	public static int ProcessArguments(string[] args)
	{
		var host = args[1];
		int port;
		if (!int.TryParse(args[2], out port))
			port = 443;
		var protocols = ((SslProtocols[])Enum.GetValues(typeof(SslProtocols))).ToList();
		var ips = GetHostAddresses(host);
		Console.Write("{0} {1}:{2}\r\n\r\n", ips, host, port);
		protocols.Remove(SslProtocols.Default);
		protocols.Remove(SslProtocols.None);
		for (int i = 0; i < protocols.Count; i++)
		{
			result = new Result();
			// Enable TLS 1.1, 1.2 and 1.3
			var Tls11 = 0x0300;
			var Tls12 = 0x0C00;
			var Tls13 = 0x3000;
			ServicePointManager.SecurityProtocol |= (SecurityProtocolType)(Tls11 | Tls12 | Tls13);
			//ServicePointManager.ServerCertificateValidationCallback = ValidateServerCertificate;
			var protocol = protocols[i];
			if (Progress != null)
				Progress.Invoke(string.Format("Test {0} {1}/{2}...", protocol.ToString().ToUpper(), i + 1, protocols.Count));
			result.Protocol = protocol;
			result.CertificateError = null;
			var connected = false;
			Exception ex = null;
			try
			{

				// if SMTP, POP3 or IMAP then...
				if (port == 25 || port == 110 || port == 143)
					TestStarTLS(host, port, protocol, out connected);
				else
					TestTCP(host, port, protocol, out connected);
			}
			catch (Exception ex1)
			{
				ex = ex1;
			}
			var noError = ex == null;
			result.Success = noError;
			Console.Write(
				"  {0,-5} = {1,-5}",
				protocol, noError);
			var extra = noError
				? string.Format(" | Exchange = {0,-5} | Cipher = {1,-5} | Hash = {2,-6}",
					result.ExchangeAlgorithm, result.CipherAlgorithm, result.HashAlgorithm)
				: string.Format(" | {0}", ex.Message);
			Console.WriteLine(extra);
			var certEx = result.CertificateError;
			if (result.CertificateError != null)
			{
				var foreDefault = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.DarkYellow;
				Console.WriteLine();
				Console.WriteLine("        " + certEx.Message);
				foreach (var key in ex.Data.Keys)
					Console.WriteLine("        {0}: {1}", key, ex.Data[key]);
				Console.ForegroundColor = foreDefault;
			}
			// If failed TCP connect then no point of continuing.
			if (!connected)
				break;
			Results.Add(result);
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

	public static string GetHostAddresses(string host, AddressFamily? addressFamily = null)
	{
		string ips = null;
		try
		{
			var ipaddress = Dns.GetHostAddresses(host);
			var address = ipaddress
				.Where(x => x.AddressFamily == (addressFamily ?? AddressFamily.InterNetwork))
				// Exclude The 169 IP range of addresses is reserved by Microsoft for private network addressing
				.Where(x => x.GetAddressBytes()[0] != 169)
				.OrderBy(x => x.ToString())
				.ToArray()
				.FirstOrDefault();
			ips = string.Join(" ", address);
		}
		catch { }
		return ips;
	}

	static bool TestTCP(string host, int port, SslProtocols protocol, out bool connected)
	{
		var success = false;
		var client = new TcpClient();
		var asyncResult = client.BeginConnect(host, port, null, null);
		// 5 seconds timeout.
		connected = asyncResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
		// Connected.
		if (connected)
		{
			var stream = client.GetStream();
			// Don't dispose underlying stream.
			using (var sslStream = new SslStream(stream, true, ValidateServerCertificate))
			{
				sslStream.ReadTimeout = 15000;
				sslStream.WriteTimeout = 15000;
				sslStream.AuthenticateAsClient(host, null, protocol, false);
				result.UpdateFromSslStream(sslStream);
				success = true;
			}
			client.EndConnect(asyncResult);
		}
		return success;
	}

	static bool TestStarTLS(string host, int port, SslProtocols protocol, out bool connected)
	{
		var success = false;
		var client = new TcpClient();
		var asyncResult = client.BeginConnect(host, port, null, null);
		// 3 seconds timeout
		connected = asyncResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
		// Connected.
		if (connected)
		{
			var stream = client.GetStream();
			using (var clearTextReader = new StreamReader(stream, System.Text.Encoding.ASCII, true, 4096, true))
			using (var clearTextWriter = new StreamWriter(stream, System.Text.Encoding.ASCII, 4096, true) { AutoFlush = true })
			using (var sslStream = new SslStream(stream, true, ValidateServerCertificate))
			{
				// SMTP
				if (port == 25)
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
					sslStream.AuthenticateAsClient(host, null, protocol, false);
					success = true;
					result.UpdateFromSslStream(sslStream);
					//using (var reader = new StreamReader(sslStream))
					//using (var writer = new StreamWriter(sslStream) { AutoFlush = true })
					//{
					//	writer.WriteLine("EHLO " + host);
					//	Console.WriteLine(reader.ReadLine());
					//}
				}
				// POP3: https://datatracker.ietf.org/doc/html/rfc2595
				if (port == 110)
				{
					var connectResponse = clearTextReader.ReadLine();
					if (!connectResponse.StartsWith("+OK"))
						throw new InvalidOperationException("POP3 Server did not respond to connection request");
					// STARTTLS
					clearTextWriter.WriteLine("STLS");
					var startTlsResponse = clearTextReader.ReadLine();
					if (!startTlsResponse.StartsWith("+OK"))
						throw new InvalidOperationException("POP3 Server did not respond to STLS request");
					sslStream.AuthenticateAsClient(host, null, protocol, false);
					success = true;
					result.UpdateFromSslStream(sslStream);
				}
				// IMAP: https://tools.ietf.org/html/rfc2595
				if (port == 143)
				{
					var connectResponse = clearTextReader.ReadLine();
					if (!connectResponse.StartsWith("+OK"))
						throw new InvalidOperationException("IMAP Server did not respond to connection request");
					// STARTTLS
					clearTextWriter.WriteLine("STLS");
					var startTlsResponse = clearTextReader.ReadLine();
					if (!startTlsResponse.StartsWith("+OK"))
						throw new InvalidOperationException("IMAP Server did not respond to STLS request");
					sslStream.AuthenticateAsClient(host, null, protocol, false);
					success = true;
					result.UpdateFromSslStream(sslStream);
				}
			}
			client.EndConnect(asyncResult);
		}
		return success;
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
		// Create new object, because original will be disposed.
		var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certificate);
		result.UpdateFromCertificate(cert, sslPolicyErrors);
		// No errors were found.
		if (sslPolicyErrors == SslPolicyErrors.None)
		{
			// Allow this client to communicate with unauthenticated servers.
			return true;
		}
		var allow = true;
		var message = string.Format("Certificate error: {0}", sslPolicyErrors);
		var ex = new Exception(message);
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
			ex.Data.Add("Certificate.Subject", certificate.Subject);
			ex.Data.Add("Certificate.Issuer", certificate.Issuer);
			ex.Data.Add("Certificate.Serial", certificate.GetSerialNumberString());
			ex.Data.Add("Certificate.Expiration", certificate.GetExpirationDateString());
		}
		if (chain != null)
		{
			for (int i = 0; i < chain.ChainStatus.Length; i++)
			{
				ex.Data.Add("Chain.ChainStatus(" + i + ")", string.Format("{0}, {1}", chain.ChainStatus[i].Status, chain.ChainStatus[i].StatusInformation));
			}
		}
		result.CertificateError = ex;
		// Allow (or not allow depending on setting value) this client to communicate with unauthenticated servers.
		return allow;
	}


	#endregion


}

