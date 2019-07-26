using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
			line = lines[i].Trim();
			if (string.IsNullOrEmpty(line))
				continue;
			var names = new string[] { line, "www." + line };
			foreach (var name  in names)
			{
				Console.WriteLine(name);
				var info = new DomainInfo();
				info.Name = name;
				//-------------------------------------------------
				info.Address = string.Join("|", CheckDns(info.Name));
				if (!string.IsNullOrEmpty(info.Address))
				{
					info.Ping = Ping(info.Name);
					info.HTTP = IsPortOpen(info.Name, 80, 4000);
					if (info.HTTP)
					{
						info.HTTP_Status = CheckHttp("http://" + info.Name);
						info.HTTP_Redirect = CheckRedirect("http://" + info.Name);
					}
					info.HTTPS = IsPortOpen(info.Name, 443, 4000);
					if (info.HTTPS)
					{
						info.HTTPS_Status = CheckHttp("https://" + info.Name);
						info.HTTPS_Redirect = CheckRedirect("https://" + info.Name);
					}
				}
				//-------------------------------------------------
				infos.Add(info);
			}
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
		public string HTTP_Status;
		public string HTTP_Redirect;
		public bool HTTPS;
		public string HTTPS_Status;
		public string HTTPS_Redirect;

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
				.Select(x => string.Format("{0}", x))
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
			Console.Write("  DNS... ");
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

	#region Check Ping

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

	#region Check Port

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

	#region Check HTTP[S]

	public static string CheckHttp(string uri)
	{
		System.Net.ServicePointManager.ServerCertificateValidationCallback = ValidateServerCertificate;
		// Test HTTP/HTTPS request.
		var start = DateTime.Now;
		try
		{
			// Check if web service page works.
			Console.Write("  Open {0}", uri);
			var request = WebRequest.Create(uri);
			// CWE-918: Server-Side Request Forgery (SSRF).
			// Note: External users do not have control over request URL.
			var response = (HttpWebResponse)request.GetResponse();
			var code = (int)response.StatusCode;
			var description = response.StatusDescription;
			response.Close();
			Console.WriteLine("  Response Status: {0} - {1}", code, description);
			return string.Format("{0} - {1}", code, description);
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
			return ex.Message;
		}
	}

	static string CheckRedirect(string url)
	{
		Console.Write("  Redirect... ");
		var finalUrl = GetFinalRedirect(url);
		Console.WriteLine("{0}", finalUrl);
		return finalUrl;
	}

	/// <remarks>https://stackoverflow.com/questions/704956/getting-the-redirected-url-from-the-original-url</remarks>
	public static string GetFinalRedirect(string url)
	{
		if (string.IsNullOrWhiteSpace(url))
			return url;

		int maxRedirCount = 8;  // prevent infinite loops
		string newUrl = url;
		do
		{
			HttpWebRequest req = null;
			HttpWebResponse resp = null;
			try
			{
				req = (HttpWebRequest)HttpWebRequest.Create(url);
				req.Method = "HEAD";
				req.AllowAutoRedirect = false;
				resp = (HttpWebResponse)req.GetResponse();
				switch (resp.StatusCode)
				{
					case HttpStatusCode.OK:
						return newUrl;
					case HttpStatusCode.Redirect:
					case HttpStatusCode.MovedPermanently:
					case HttpStatusCode.RedirectKeepVerb:
					case HttpStatusCode.RedirectMethod:
						newUrl = resp.Headers["Location"];
						if (newUrl == null)
							return url;

						if (newUrl.IndexOf("://", System.StringComparison.Ordinal) == -1)
						{
							// Doesn't have a URL Schema, meaning it's a relative or absolute URL
							Uri u = new Uri(new Uri(url), newUrl);
							newUrl = u.ToString();
						}
						break;
					default:
						return newUrl;
				}
				url = newUrl;
			}
			catch (WebException)
			{
				// Return the last known good URL
				return newUrl;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				return null;
			}
			finally
			{
				if (resp != null)
					resp.Close();
			}
		} while (maxRedirCount-- > 0);
		return newUrl;
	}

	#endregion

	#region Ignore invalid SSL Certificate

	/// <summary>
	/// The following method is invoked by the RemoteCertificateValidationDelegate.
	/// Net.ServicePointManager.ServerCertificateValidationCallback = AddressOf ValidateServerCertificate
	/// </summary>
	/// <remarks>
	/// Add "AllowCertificateErrors" to allow certificate errors: request.Headers.Add("AllowCertificateErrors");
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
		// Allow (or not allow depending on setting value) this client to communicate with unauthenticated servers.
		return allow;
	}


	#endregion

}

