// @under-test: Tester/Scripts/IsPortOpen/IsPortOpen.ps1
// @area: scripts   @layer: integration
using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JocysCom.Shell.Scripts.Tester.Tests
{
	[TestClass]
	public class IsPortOpenScriptIntegrationTests
	{
		[TestMethod]
		public void Closed_port_returns_exit_code_1_and_prints_CLOSED()
		{
			var (exit, stdout) = RunIsPortOpen("-Computer 127.0.0.1 -Port 1 -TimeoutMs 300");
			Assert.AreEqual(1, exit, "Expected 'closed' exit code 1. Output was: " + stdout);
			StringAssert.Contains(stdout, "CLOSED");
		}

		[TestMethod]
		public void Quiet_switch_suppresses_output()
		{
			var (_, stdout) = RunIsPortOpen("-Computer 127.0.0.1 -Port 1 -TimeoutMs 300 -Quiet");
			Assert.AreEqual("", stdout.Trim());
		}

		static (int ExitCode, string StdOut) RunIsPortOpen(string extraArgs)
		{
			Assert.IsTrue(File.Exists(TestPaths.IsPortOpenPs1),
				"Fixture not found: " + TestPaths.IsPortOpenPs1);

			var psi = new ProcessStartInfo("powershell.exe",
				$"-NoProfile -ExecutionPolicy Bypass -File \"{TestPaths.IsPortOpenPs1}\" {extraArgs}")
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			using var proc = Process.Start(psi)!;
			var stdout = proc.StandardOutput.ReadToEnd();
			if (!proc.WaitForExit(TimeSpan.FromSeconds(15)))
			{
				proc.Kill(true);
				Assert.Fail("IsPortOpen.ps1 did not exit within 15 s");
			}
			return (proc.ExitCode, stdout);
		}
	}
}
