using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

public class RSA_for_SQL
{

	// Part of article: https://github.com/JocysCom/ShellScripts/wiki/RSA-for-SQL

	public static int ProcessArguments(params string[] args)
	{
		// Generate new self signed certificate.
		var name = "name@domain.com";
		//name = WindowsIdentity.GetCurrent().Name;
		Console.WriteLine("Generating Certificate for: {0}", name);
		var properties = new CertificateHelper.SelfSignedCertProperties(name);
		var cert = CertificateHelper.CreateSelfSignedCertificate(properties);
		var priBin = "Store\\" + name + ".pfx";
		var pubBin = "Store\\" + name + ".PublicKey.cer";
		var priPem = "Store\\" + name + ".pem";
		var pubPem = "Store\\" + name + ".PublicKey.pem";
		string privateKeyPassword = null;
		if (cert != null)
		{
			CertificateHelper.ExportPrivateKey(cert, priBin, false, privateKeyPassword);
			CertificateHelper.ExportPrivateKey(cert, priPem, true, privateKeyPassword);
			CertificateHelper.ExportPublicKey(cert, pubBin, false);
			CertificateHelper.ExportPublicKey(cert, pubPem, true);
		}
		// Encryption test.
		var text = "Test";
		Console.WriteLine("Encrypt: {0}", text);
		var encrypted = CertificateHelper.Encrypt(pubBin, "Test", null, true);
		Console.WriteLine(encrypted);
		// Decryption test.
		var decrypted = CertificateHelper.Decrypt(priBin, encrypted, privateKeyPassword);
		Console.WriteLine("Decrypted: {0}", decrypted);
		Console.WriteLine("Done");
		return 0;
	}

	public class CertificateHelper
	{
		// Example:
		//
		//// Generate new self signed certificate.
		//var name = "name@domain.com";
		////name = WindowsIdentity.GetCurrent().Name;
		//Console.WriteLine("Generating Certificate for: {0}", name);
		//var properties = new SelfSignedCertProperties(name);
		//var cert = CreateSelfSignedCertificate(properties);
		//var priBin = "Store\\" + name + ".pfx";
		//var pubBin = "Store\\" + name + ".PublicKey.cer";
		//var priPem = "Store\\" + name + ".pem";
		//var pubPem = "Store\\" + name + ".PublicKey.pem";
		//string privateKeyPassword = null;
		//if (cert != null)
		//{
		//	ExportPrivateKey(cert, priBin, false, privateKeyPassword);
		//	ExportPrivateKey(cert, priPem, true, privateKeyPassword);
		//	ExportPublicKey(cert, pubBin, false);
		//	ExportPublicKey(cert, pubPem, true);
		//}
		//// Encryption test.
		//var text = "Test";
		//Console.WriteLine("Encrypt: {0}", text);
		//var encrypted = Encrypt(pubBin, "Test", null, true);
		//Console.WriteLine(encrypted);
		//// Decryption test.
		//var decrypted = Decrypt(priBin, encrypted, privateKeyPassword);
		//Console.WriteLine("Decrypted: {0}", decrypted);
		//Console.WriteLine("Done");

		//// Requires .NET Framework 4.7.2
		//// Namespace: System.Security.Cryptography.X509Certificates
		//// Assemblies: System.Security.Cryptography.X509Certificates.dll
		//
		//public static void GenerateCertificate(string name)
		//{
		//	var rsa = RSA.Create("RSA_2048");
		//	var distinguishedName = new X500DistinguishedName("CN=Message_" + name);
		//	var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		//	var endEntityTypicalUsages =
		//		X509KeyUsageFlags.DataEncipherment |
		//		X509KeyUsageFlags.KeyEncipherment |
		//		X509KeyUsageFlags.DigitalSignature;
		//	request.CertificateExtensions.Add(new X509KeyUsageExtension(endEntityTypicalUsages, true));
		//	request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
		//	var cert = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5));
		//}

		/// <summary>Export private key as name.PrivateKey.pfx or name.PrivateKey.pem.</summary>
		public static void ExportPrivateKey(X509Certificate2 cert, string fileName, bool ascii = false, string password = null)
		{
			Export(cert, fileName, ascii, true, password);
		}

		/// <summary>Export public key as name.PublicKey.cer or name.PublicKey.pem.</summary>
		public static void ExportPublicKey(X509Certificate2 cert, string fileName, bool ascii = false)
		{
			Export(cert, fileName, ascii);
		}

		static void Export(X509Certificate2 cert, string fileName, bool ascii = false, bool includePrivateKey = false, string privateKeyPassword = null)
		{
			var fi = new FileInfo(fileName);
			if (!fi.Directory.Exists)
				fi.Directory.Create();
			var certType = includePrivateKey ? X509ContentType.Pfx : X509ContentType.Cert;
			var bytes = cert.Export(certType, privateKeyPassword);
			if (ascii)
			{
				var type = includePrivateKey ? "RSA PRIVATE KEY" : "CERTIFICATE";
				var base64 = Convert.ToBase64String(bytes, Base64FormattingOptions.InsertLineBreaks);
				var sb = new StringBuilder();
				sb.AppendLine("-----BEGIN " + type + "-----");
				sb.AppendLine(base64);
				sb.AppendLine("-----END " + type + "-----");
				File.WriteAllText(fileName, sb.ToString(), Encoding.ASCII);
			}
			else
			{
				File.WriteAllBytes(fileName, bytes);
			}
		}

		/// <summary>
		/// Encrypt string to base64 string.
		/// </summary>
		/// <param name="fileName">File name which contains Public key. You can also use the PFX here as it contains the private key.</param>
		/// <param name="input">Encrypted base64 string.</param>
		/// <param name="privateKeyPassword">Optional private key password.</param>
		/// <param name="addHeaders">Wrap base64 between BEGIN DATA header and END DATA footer.</param>
		/// <returns>Encrypted text</returns>
		public static string Encrypt(string fileName, string input, string privateKeyPassword = null, bool addHeaders = false)
		{
			var cert = new X509Certificate2(fileName, privateKeyPassword, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
			var inputBytes = Encoding.Unicode.GetBytes(input);
			var bytes = Encrypt(cert, inputBytes);
			string data = "";
			if (addHeaders)
				data += "-----BEGIN DATA-----\r\n";
			data += Convert.ToBase64String(bytes, Base64FormattingOptions.InsertLineBreaks);
			if (addHeaders)
				data += "\r\n-----END DATA-----";
			return data;
		}

		public static byte[] Encrypt(X509Certificate2 cert, byte[] input)
		{
			using (var cryptoProvider = cert.PublicKey.Key as RSACryptoServiceProvider)
			{
				var bytes = cryptoProvider.Encrypt(input, true);
				return bytes;
			}
		}

		/// <summary>
		/// Decrypt encrypted base64 string. 
		/// </summary>
		/// <param name="fileName">File name which contains private key.</param>
		/// <param name="base64">Encrypted base64 string.</param>
		/// <param name="privateKeyPassword">Optional private key password.</param>
		/// <returns>Decrypted string.</returns>
		public static string Decrypt(string fileName, string base64, string privateKeyPassword = null)
		{
			var cert = new X509Certificate2(fileName, privateKeyPassword, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
			// Strip header and footer.
			var headers = new System.Text.RegularExpressions.Regex("[-]{5,}[ A-Z]+[-]{5,}");
			base64 = headers.Replace(base64, "").Trim('\r', '\n', ' ');
			// Decrypt.
			var input = Convert.FromBase64String(base64);
			var bytes = Decrypt(cert, input);
			var ascii = Encoding.Unicode.GetString(bytes);
			return ascii;
		}

		/// <summary>
		/// Decrypt bytes string.
		/// </summary>
		/// <param name="cert">Certificate which contains private key.</param>
		/// <param name="input">Encrypted bytes.</param>
		/// <returns>Decrypted bytes.</returns>
		public static byte[] Decrypt(X509Certificate2 cert, byte[] input)
		{
			using (var cryptoProvider = cert.PrivateKey as RSACryptoServiceProvider)
			{
				var bytes = cryptoProvider.Decrypt(input, true);
				return bytes;
			}
		}

		internal class NativeMethods
		{

			[DllImport("Crypt32.dll", SetLastError = true, ExactSpelling = true)]
			internal static extern IntPtr CertCreateSelfSignCertificate(
			   IntPtr providerHandle,
			   [In] CryptoApiBlob subjectIssuerBlob,
			   int flags,
			   ref CRYPT_KEY_PROV_INFO pKeyProvInfo,
			   ref CRYPT_ALGORITHM_IDENTIFIER pSignatureAlgorithm,
			   [In] SystemTime startTime,
			   [In] SystemTime endTime,
			   IntPtr extensions);

			[DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool FileTimeToSystemTime(
			   [In] ref long fileTime,
			   [Out] SystemTime systemTime);

			[StructLayout(LayoutKind.Sequential)]
			internal class CryptoApiBlob
			{
				public int DataLength;
				public IntPtr Data;

				public CryptoApiBlob(int dataLength, IntPtr data)
				{
					DataLength = dataLength;
					Data = data;
				}
			}

			[StructLayout(LayoutKind.Sequential)]
			internal class SystemTime
			{
				public short Year;
				public short Month;
				public short DayOfWeek;
				public short Day;
				public short Hour;
				public short Minute;
				public short Second;
				public short Milliseconds;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct CRYPT_KEY_PROV_INFO
			{
				[MarshalAs(UnmanagedType.LPWStr)]
				public string pwszContainerName;
				[MarshalAs(UnmanagedType.LPWStr)]
				public string pwszProvName;
				public uint dwProvType;
				public uint dwFlags;
				public uint cProvParam;
				public IntPtr rgProvParam;
				public uint dwKeySpec;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct CRYPT_ALGORITHM_IDENTIFIER
			{
				[MarshalAs(UnmanagedType.LPStr)]
				public string pszObjId;
				public CRYPTOAPI_BLOB parameters;
			}

			[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
			public struct CRYPTOAPI_BLOB
			{
				public uint cbData;
				public IntPtr pbData;
			}

		}

		public const string OID_RSA_SHA256RSA = "1.2.840.113549.1.1.11";
		public const string szOID_ENHANCED_KEY_USAGE = "2.5.29.37";

		public class SelfSignedCertProperties
		{
			public DateTime ValidFrom { get; set; }
			public DateTime ValidTo { get; set; }
			public X500DistinguishedName Name { get; set; }
			public int KeyBitLength { get; set; }
			public bool IsPrivateKeyExportable { get; set; }
			public SelfSignedCertProperties(string name = "self")
			{
				IsPrivateKeyExportable = true;
				var today = DateTime.Today;
				ValidFrom = today.AddDays(-1);
				ValidTo = today.AddYears(10);
				Name = new X500DistinguishedName("cn=" + name);
				KeyBitLength = 4096;
			}
		}

		public static X509Certificate2 CreateSelfSignedCertificate(SelfSignedCertProperties properties)
		{
			//GenerateSignatureKey(properties.IsPrivateKeyExportable, properties.KeyBitLength);
			var asnName = properties.Name.RawData;
			var asnNameHandle = GCHandle.Alloc(asnName, GCHandleType.Pinned);
			var keySize = properties.KeyBitLength;
			if (keySize <= 0)
				keySize = 2048; // Min keysize
			var algoritm = OID_RSA_SHA256RSA;
			var parameters = new CspParameters()
			{
				ProviderName = "Microsoft Enhanced RSA and AES Cryptographic Provider",
				ProviderType = 24,
				KeyContainerName = Guid.NewGuid().ToString(),
				KeyNumber = (int)KeyNumber.Exchange,
				Flags = CspProviderFlags.UseMachineKeyStore
			};
			try
			{
				var signatureAlgorithm = new NativeMethods.CRYPT_ALGORITHM_IDENTIFIER
				{
					pszObjId = algoritm
				};
				signatureAlgorithm.parameters.cbData = 0;
				signatureAlgorithm.parameters.pbData = IntPtr.Zero;
				using (new RSACryptoServiceProvider(keySize, parameters))
				{
					var providerInfo = new NativeMethods.CRYPT_KEY_PROV_INFO
					{
						pwszProvName = parameters.ProviderName,
						pwszContainerName = parameters.KeyContainerName,
						dwProvType = (uint)parameters.ProviderType,
						dwFlags = 0x20, //(uint)parameters.Flags, 
						dwKeySpec = (uint)parameters.KeyNumber
					};
					IntPtr certHandle = NativeMethods.CertCreateSelfSignCertificate(
					  IntPtr.Zero,
					  new NativeMethods.CryptoApiBlob(asnName.Length, asnNameHandle.AddrOfPinnedObject()),
					  0,
					  ref providerInfo,
					  ref signatureAlgorithm,
					  ToSystemTime(properties.ValidFrom),
					  ToSystemTime(properties.ValidTo),
					  IntPtr.Zero);
					if (IntPtr.Zero == certHandle)
						ThrowExceptionIfGetLastErrorIsNotZero();
					return new X509Certificate2(certHandle);
				}
			}
			finally
			{
				// Free the unmanaged memory.
				asnNameHandle.Free();
			}
		}

		private static NativeMethods.SystemTime ToSystemTime(DateTime dateTime)
		{
			long fileTime = dateTime.ToFileTime();
			var systemTime = new NativeMethods.SystemTime();
			if (!NativeMethods.FileTimeToSystemTime(ref fileTime, systemTime))
				ThrowExceptionIfGetLastErrorIsNotZero();
			return systemTime;
		}

		internal static void ThrowExceptionIfGetLastErrorIsNotZero()
		{
			int win32ErrorCode = Marshal.GetLastWin32Error();
			if (win32ErrorCode == 0)
				return;
			if (win32ErrorCode > 0)
				win32ErrorCode = (int)((((uint)win32ErrorCode) & 0x0000FFFF) | 0x80070000U);
			Marshal.ThrowExceptionForHR(win32ErrorCode);
		}

	}

	/// <summary>
	/// Microsoft Private Key Format helper. PVK is a proprietary Microsoft format
	/// that stores a cryptographic private key and can be password-protected.
	/// PVK files can be used in Microsoft SQL Server certificate operations.
	/// </summary>
	public class PrivateKeyHelper
	{

		const uint _Magic = 0xb0b5f11e;

		/// <summary>
		/// Convert PVK file bytes to RSACryptoServiceProvider.
		/// </summary>
		/// <param name="pvk">PVK File bytes.</param>
		/// <param name="password">Optional password.</param>
		/// <param name="weak">Weak encryption option (US export restrictions)</param>
		/// <returns>RSACryptoServiceProvider</returns>
		public static RSACryptoServiceProvider Convert(byte[] pvk, string password = null, bool weak = false)
		{
			var br = new BinaryReader(new MemoryStream(pvk));
			var magic = br.ReadUInt32();
			if (magic != _Magic)
				return null;
			var reserved = br.ReadUInt32();
			if (reserved != 0x0)
				return null;
			var rsa = new RSACryptoServiceProvider();
			var keyType = br.ReadInt32();
			var encrypted = br.ReadUInt32() == 1;
			var saltLength = br.ReadInt32();
			var keyLength = br.ReadInt32();
			byte[] salt = null;
			// If salt is present i.e. key is encrypted then...
			if (saltLength > 0)
				salt = br.ReadBytes(saltLength);
			var keyBlob = br.ReadBytes(keyLength);
			if (saltLength > 0 && encrypted)
			{
				var secretKey = DeriveKey(salt, password);
				// If weak encryption is enabled due to US export restrictions then...
				if (weak)
					// Truncate 128-bit key to 40 bits.
					Array.Clear(secretKey, 5, 11);
				// 8 byte header part of the BLOB is not encrypted.
				Transform(secretKey, keyBlob, 8, keyBlob.Length - 8, keyBlob, 8);
				// Cleanup.
				Array.Clear(salt, 0, salt.Length);
				Array.Clear(secretKey, 0, secretKey.Length);
			}
			rsa.ImportCspBlob(keyBlob);
			// Cleanup key pair, which may include an unencrypted key pair.
			Array.Clear(keyBlob, 0, keyBlob.Length);
			return rsa;
		}

		/// <summary>
		/// Convert RSACryptoServiceProvider to PVK file bytes.
		/// </summary>
		/// <param name="rsa">RSACryptoServiceProvider</param>
		/// <param name="password">Optional password</param>
		/// <param name="weak">Weak encryption option (US export restrictions)</param>
		/// <returns>PVK File bytes</returns>
		public static byte[] Convert(RSACryptoServiceProvider rsa, string password = null, bool weak = false)
		{
			var ms = new MemoryStream();
			var fs = new BinaryWriter(ms);
			int keyType = 2;
			int reserved = 0;
			// header
			byte[] empty = new byte[4];
			fs.Write(_Magic);
			fs.Write(reserved);
			fs.Write(keyType);
			var encrypted = !string.IsNullOrEmpty(password);
			fs.Write(encrypted ? 1 : 0);
			var saltlen = encrypted ? 16 : 0;
			fs.Write(saltlen);
			var keyBlob = rsa.ExportCspBlob(true);
			var keylen = keyBlob.Length;
			fs.Write(keylen);
			if (encrypted)
			{
				var salt = new byte[saltlen];
				// generate new salt (16 bytes)
				var rng = RandomNumberGenerator.Create();
				rng.GetBytes(salt);
				fs.Write(salt);
				var secretKey = DeriveKey(salt, password);
				// If weak encryption is enabled due to US export restrictions then...
				if (weak)
					Array.Clear(secretKey, 5, 11);
				// 8 byte header part of the BLOB is not encrypted.
				Transform(secretKey, keyBlob, 8, keyBlob.Length - 8, keyBlob, 8);
				// Cleanup.
				Array.Clear(salt, 0, salt.Length);
				Array.Clear(secretKey, 0, secretKey.Length);
			}
			fs.Write(keyBlob, 0, keyBlob.Length);
			// Cleanup BLOB, which may include an unencrypted key pair.
			Array.Clear(keyBlob, 0, keyBlob.Length);
			fs.Flush();
			var pvk = ms.ToArray();
			fs.Close();
			return pvk;
		}

		static byte[] DeriveKey(byte[] salt, string password)
		{
			var pwd = Encoding.ASCII.GetBytes(password);
			var sha1 = SHA1.Create();
			sha1.TransformBlock(salt, 0, salt.Length, salt, 0);
			sha1.TransformFinalBlock(pwd, 0, pwd.Length);
			var key = new byte[16];
			Buffer.BlockCopy(sha1.Hash, 0, key, 0, 16);
			sha1.Clear();
			Array.Clear(pwd, 0, pwd.Length);
			return key;
		}

		#region RC4 Encryption/Decryption

		static void Transform(byte[] key, byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
		{
			var data = new byte[inputCount];
			Array.Copy(inputBuffer, inputOffset, data, 0, data.Length);
			var output = EncryptOutput(key, data).ToArray();
			Array.Copy(output, 0, outputBuffer, outputOffset, output.Length);
		}

		static byte[] EncryptInitalize(byte[] key)
		{
			byte[] s = Enumerable.Range(0, 256)
			  .Select(i => (byte)i)
			  .ToArray();
			for (int i = 0, j = 0; i < 256; i++)
			{
				j = (j + key[i % key.Length] + s[i]) & 255;
				Swap(s, i, j);
			}
			return s;
		}

		static byte[] EncryptOutput(byte[] key, byte[] data)
		{
			byte[] s = EncryptInitalize(key);
			int i = 0;
			int j = 0;
			return data.Select((b) =>
			{
				i = (i + 1) & 255;
				j = (j + s[i]) & 255;
				Swap(s, i, j);
				return (byte)(b ^ s[(s[i] + s[j]) & 255]);
			}).ToArray();
		}

		static void Swap(byte[] s, int i, int j)
		{
			byte c = s[i];
			s[i] = s[j];
			s[j] = c;
		}

		#endregion
	}

}
