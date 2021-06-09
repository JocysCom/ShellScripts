using System;
using System.Linq;

public class HMAC_for_SQL
{

	// Part of article: https://github.com/JocysCom/ShellScripts/wiki/HMAC-for-SQL

	public static int ProcessArguments(string[] args)
	{
		// Use Unicode, because ASCII doesn't work worldwide.
		var base64 = HashPassword("Password", 128);
		var isValid = IsValidPassword("Password", base64);
		Console.WriteLine("Results:");
		Console.WriteLine("  IsValid: {0}, base64: {1}", isValid, base64);
		Console.WriteLine();
		Console.WriteLine("-------");
		var key = StringToByteArray("0x63727970746969");
		var data = StringToByteArray("0x68656C6C6F21");
		var algorithm = new System.Security.Cryptography.HMACSHA256();
		algorithm.Key = key;
		var hash = algorithm.ComputeHash(data);
		Console.WriteLine("  Hash: {0}", string.Join("", hash.Select(x => x.ToString("X2"))));
		return 0;
	}

	public static byte[] StringToByteArray(string hex)
	{
		if (hex.StartsWith("0x"))
			hex = hex.Substring(2);
		hex = hex.Replace(":", "").Replace("-", "");
		var chars = hex.Length;
		var bytes = new byte[chars / 2];
		for (int i = 0; i < chars; i += 2)
			bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
		return bytes;
	}

	/// <summary>Hash new password.</summary>
	public static string HashPassword(string password, int security = 256)
	{
		// You can limit security to 128-bit which will produce
		// base64 string, which will fit into a varchar(44) field on the database.
		// This will allow to store encrypted password in old password field if its size is limited.
		var size = security / 8;
		var algorithm = new System.Security.Cryptography.HMACSHA256();
		// ----------------------------------------------------------------
		// Convert string to bytes.
		// Use Unicode, because ASCII doesn't work worldwide and SQL server doesn't support UTF8.
		var bytes = System.Text.Encoding.Unicode.GetBytes(password);
		// Generate random salt.
		var salt = new byte[size];
		var generator = System.Security.Cryptography.RandomNumberGenerator.Create();
		generator.GetBytes(salt);
		// Compute hash.
		algorithm.Key = salt;
		var hash = algorithm.ComputeHash(bytes);
		// Combine salt and hash and convert to HEX.
		var baseBytes = new byte[size * 2];
		Array.Copy(salt, 0, baseBytes, 0, size);
		Array.Copy(hash, 0, baseBytes, size, size);
		Console.WriteLine("HashPassword:");
		Console.WriteLine("  Salt: {0}", string.Join("", salt.Select(x => x.ToString("X2"))));
		Console.WriteLine("  Hash: {0}", string.Join("", hash.Take(size).Select(x => x.ToString("X2"))));
		// Convert salt and hash to Base64 string.
		var base64 = System.Convert.ToBase64String(baseBytes);
		return base64;
	}

	public static bool IsValidPassword(string password, string base64)
	{
		// ----------------------------------------------------------------
		if (string.IsNullOrEmpty(password))
			return false;
		if (string.IsNullOrEmpty(base64))
			return false;
		// Try parse salt and hash from base64.
		byte[] baseBytes;
		try { baseBytes = System.Convert.FromBase64String(base64); }
		catch { return false; }
		// Get size of salt and hash.
		var size = baseBytes.Length / 2;
		var salt = new byte[size];
		var hash = new byte[size];
		Array.Copy(baseBytes, 0, salt, 0, size);
		Array.Copy(baseBytes, size, hash, 0, size);
		Console.WriteLine("IsValidPassword:");
		Console.WriteLine("  Salt: {0}", string.Join("", salt.Select(x => x.ToString("X2"))));
		Console.WriteLine("  Hash: {0}", string.Join("", hash.Take(size).Select(x => x.ToString("X2"))));
		// ----------------------------------------------------------------
		// Convert string to bytes.
		// Use Unicode, because ASCII doesn't work worldwide and SQL server doesn't support UTF8.
		var passwordBytes = System.Text.Encoding.Unicode.GetBytes(password);
		var algorithm = new System.Security.Cryptography.HMACSHA256();
		algorithm.Key = salt;
		var passwordHash = algorithm.ComputeHash(passwordBytes);
		// Compare first specified bytes.
		for (int i = 0; i < size; i++)
		{
			if (passwordHash[i] != hash[i])
				// Password hash bytes do not match.
				return false;
		}
		// Password hash bytes match.
		return true;
	}

}

