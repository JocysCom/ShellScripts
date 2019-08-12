$CodeDefinition=@"
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Code
{
    public class Methods 
    {

		static string password = "";
	
        public static void GenerateCertificate(string name)
        {
            var rsa = RSA.Create(2048);
			var distinguishedName = new X500DistinguishedName("CN=Message_"+name);
			var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			var endEntityTypicalUsages =
				X509KeyUsageFlags.DataEncipherment |
				X509KeyUsageFlags.KeyEncipherment |
				X509KeyUsageFlags.DigitalSignature;
			request.CertificateExtensions.Add(new X509KeyUsageExtension(endEntityTypicalUsages, true));
			request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
            var cert = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5));

            var pem = new StringBuilder();
			pem.AppendLine("-----BEGIN RSA PRIVATE KEY-----");
			pem.AppendLine(Convert.ToBase64String(cert.Export(X509ContentType.Pfx, password), Base64FormattingOptions.InsertLineBreaks));
			pem.AppendLine("-----END RSA PRIVATE KEY-----");
			//File.WriteAllText(".\\"+name+".PrivateKey.pem", pem.ToString());
			File.WriteAllBytes(".\\"+name+".PrivateKey.pfx", cert.Export(X509ContentType.Pfx, password));
            //Console.WriteLine(pem.ToString());

            var cer = new StringBuilder();
			cer.AppendLine("-----BEGIN CERTIFICATE-----");
			cer.AppendLine(Convert.ToBase64String(cert.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks));
			cer.AppendLine("-----END CERTIFICATE-----");
            File.WriteAllText(".\\"+name+".PublicKey.cer", cer.ToString());
			Console.WriteLine(cer.ToString());
        }
		
		public static string Encrypt(string name, string data)
		{
			var cerFile = ".\\"+name+".PublicKey.cer";
			// You can also use the PFX here as it contains the private key
			var publicCertificate = new X509Certificate2(cerFile);
			using (var cryptoProvider = publicCertificate.PublicKey.Key as RSACryptoServiceProvider)
			{
				var byteData = Encoding.Unicode.GetBytes(data);
				var encryptedBytes = cryptoProvider.Encrypt(byteData, true);
				var encryptedText = Convert.ToBase64String(encryptedBytes, Base64FormattingOptions.InsertLineBreaks);
				Console.WriteLine("-----BEGIN DATA-----");
				Console.WriteLine(encryptedText);
				Console.WriteLine("-----END DATA-----");
				return encryptedText;
			}
		}
		
		public static string Decrypt(string name, string data)
		{
			var cerFile = ".\\"+name+".PrivateKey.pfx";
			// You can also use the PFX here as it contains the private key
			var privateCertificate = new X509Certificate2(cerFile, password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
			using (var cryptoProvider = privateCertificate.PrivateKey as RSACryptoServiceProvider)
			{
				var encryptedBytes = Convert.FromBase64String(data);
				var decryptedBytes = cryptoProvider.Decrypt(encryptedBytes, true);
				var decryptedText = Encoding.Unicode.GetString(decryptedBytes);
				Console.WriteLine("-----BEGIN DATA-----");
				Console.WriteLine(decryptedText);
				Console.WriteLine("-----END DATA-----");
				return decryptedText;
			}
		}

    }
}
"@
Add-Type -TypeDefinition $CodeDefinition
Remove-Variable CodeDefinition

$email = "evaldas@jocys.com"
[Code.Methods]::GenerateCertificate($email)
$en = [Code.Methods]::Encrypt($email, "Test")
$de = [Code.Methods]::Decrypt($email, $en)
#Set-Clipboard -Value $de
#$c = get-clipboard;
#$de = [Code.Methods]::Decrypt($email, $c)
#Set-Clipboard -Value $de
