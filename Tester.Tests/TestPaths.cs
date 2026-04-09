using System;
using System.IO;
using System.Linq;

namespace JocysCom.Shell.Scripts.Tester.Tests
{
	/// <summary>Shared path helpers so every test resolves the product tree identically.</summary>
	internal static class TestPaths
	{
		public static string RepoRoot { get; } = FindRepoRoot();
		public static string ScriptsRoot => Path.Combine(RepoRoot, "Tester", "Scripts");
		public static string IsPortOpenPs1 => Path.Combine(ScriptsRoot, "IsPortOpen", "IsPortOpen.ps1");

		static string FindRepoRoot()
		{
			var dir = new DirectoryInfo(AppContext.BaseDirectory);
			while (dir != null)
			{
				if (File.Exists(Path.Combine(dir.FullName, "JocysCom.ShellScripts.slnx")))
					return dir.FullName;
				dir = dir.Parent;
			}
			throw new InvalidOperationException("Could not locate repository root from " + AppContext.BaseDirectory);
		}

		public static string FindTesterExe()
		{
			var bin = Path.Combine(RepoRoot, "Tester", "bin");
			if (!Directory.Exists(bin)) throw new FileNotFoundException("Tester bin folder not found: " + bin);
			// Match whatever net*-windows TFM the product was built with (net8, net10, …).
			var candidates = Directory.EnumerateFiles(bin, "JocysCom.Shell.Scripts.Tester.exe", SearchOption.AllDirectories)
				.Where(p => p.Contains("-windows", StringComparison.OrdinalIgnoreCase))
				.OrderByDescending(File.GetLastWriteTimeUtc)
				.FirstOrDefault();
			return candidates ?? throw new FileNotFoundException("JocysCom.Shell.Scripts.Tester.exe not found under " + bin);
		}
	}
}
