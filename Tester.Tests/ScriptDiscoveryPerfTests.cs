// @under-test: Tester/MainWindow.xaml.cs
// @under-test: Tester/Scripts/IsPortOpen/IsPortOpen.ps1
// @area: perf   @layer: perf
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation.Language;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JocysCom.Shell.Scripts.Tester.Tests
{
	/// <summary>
	/// Very basic, deliberately lenient budgets so CI agents of varying speed don't flake.
	/// The intent is to catch orders-of-magnitude regressions — not to gate on microseconds.
	/// </summary>
	[TestClass]
	public class ScriptDiscoveryPerfTests
	{
		[TestMethod]
		public void Enumerating_scripts_folder_is_fast()
		{
			var sw = Stopwatch.StartNew();
			var count = Directory.EnumerateDirectories(TestPaths.ScriptsRoot)
				.SelectMany(d => Directory.EnumerateFiles(d, "*.ps1")
					.Concat(Directory.EnumerateFiles(d, "*.bat")))
				.Count();
			sw.Stop();
			Assert.IsTrue(count > 0, "Expected at least one script under " + TestPaths.ScriptsRoot);
			Assert.IsTrue(sw.ElapsedMilliseconds < 2000,
				$"Enumeration took {sw.ElapsedMilliseconds} ms (budget 2000 ms)");
		}

		[TestMethod]
		public void Parsing_IsPortOpen_ps1_twenty_times_is_fast()
		{
			// Warm-up — first parse pays JIT + assembly load tax for System.Management.Automation.
			Parser.ParseFile(TestPaths.IsPortOpenPs1, out _, out _);

			var sw = Stopwatch.StartNew();
			for (var i = 0; i < 20; i++)
				Parser.ParseFile(TestPaths.IsPortOpenPs1, out _, out _);
			sw.Stop();

			Assert.IsTrue(sw.ElapsedMilliseconds < 5000,
				$"20 parses took {sw.ElapsedMilliseconds} ms (budget 5000 ms)");
		}
	}
}
