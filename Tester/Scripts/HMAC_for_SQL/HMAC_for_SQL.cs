using System;
using System.Linq;

public class HMAC_for_SQL
{

	// Part of article: https://github.com/JocysCom/ShellScripts/wiki/HMAC-for-SQL

	public static int ProcessArguments(string[] args)
	{
		// Use Unicode, because ASCII doesn't work worldwide.
		var base64 = HashPassword("Password");
		var isValid = IsValidPassword("Password", base64);
		Console.WriteLine("Results:");
		Console.WriteLine("  IsValid: {0}, base64: {1}", isValid, base64);
		return 0;
	}

	/// <summary>Hash new password.</summary>
	public static string HashPassword(string password)
	{
		// Limit hash and salt size to 16 bytes.
		// This will produce base64 which will fit into a varchar(44) field on database.
		var size = 16;
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
		// Limit hash and salt size to 16 bytes.
		// This will produce base64 which will fit into a varchar(44) field on database.
		var size = 16;
		var algorithm = new System.Security.Cryptography.HMACSHA256();
		// ----------------------------------------------------------------
		if (string.IsNullOrEmpty(password))
			return false;
		if (string.IsNullOrEmpty(base64))
			return false;
		// Try parse salt and hash from base64.
		byte[] baseBytes;
		try { baseBytes = System.Convert.FromBase64String(base64); }
		catch { return false; }
		// Make sure size is correct.
		if (baseBytes.Length != size * 2)
			return false;
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

