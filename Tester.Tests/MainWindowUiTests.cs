// @under-test: Tester/MainWindow.xaml
// @area: ui   @layer: ui-wpf
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JocysCom.Shell.Scripts.Tester.Tests
{
	/// <summary>
	/// Launches the real Tester.exe and exercises the dynamic script discovery UI.
	/// Zero driver processes: uses in-box <c>System.Windows.Automation</c> only.
	/// </summary>
	[TestClass]
	public class MainWindowUiTests
	{
		Process _proc;
		AutomationElement _window;

		[TestInitialize]
		public void Launch()
		{
			var exe = TestPaths.FindTesterExe();
			_proc = Process.Start(new ProcessStartInfo(exe)
			{
				UseShellExecute = false,
				WorkingDirectory = Path.GetDirectoryName(exe)!,
			})!;
			_proc.WaitForInputIdle(5000);
			_window = WaitFor(() =>
			{
				_proc.Refresh();
				return _proc.MainWindowHandle == IntPtr.Zero ? null : AutomationElement.FromHandle(_proc.MainWindowHandle);
			}, TimeSpan.FromSeconds(10));
		}

		[TestCleanup]
		public void Close()
		{
			try { if (_proc != null && !_proc.HasExited) _proc.Kill(entireProcessTree: true); }
			catch { /* best effort */ }
			_proc?.Dispose();
		}

		[TestMethod, TestCategory("smoke"), TestCategory("ui-wpf")]
		public void App_launches_and_lists_IsPortOpen_ps1_in_ScriptsListBox()
		{
			Assert.IsNotNull(_window, "Main window did not appear");

			// Ensure the FolderTextBox is pointing at the product's Scripts folder.
			// The app's default locator walks up from the exe and should find it automatically,
			// but we force the value to make the test deterministic regardless of persisted settings.
			var folderBox = FindById("Main.FolderTextBox");
			SetValue(folderBox, TestPaths.ScriptsRoot);

			// There's no explicit reload on TextBox change, so click Refresh by AutomationId.
			var refreshBtn = _window.FindFirst(TreeScope.Descendants,
				new PropertyCondition(AutomationElement.NameProperty, "Refresh"));
			Assert.IsNotNull(refreshBtn, "Refresh button not found");
			((InvokePattern)refreshBtn.GetCurrentPattern(InvokePattern.Pattern)).Invoke();

			var listBox = WaitFor(() =>
			{
				var lb = FindById("Main.ScriptsListBox");
				var items = lb?.FindAll(TreeScope.Children, Condition.TrueCondition);
				return items != null && items.Count > 0 ? lb : null;
			}, TimeSpan.FromSeconds(10));
			Assert.IsNotNull(listBox, "ScriptsListBox never populated");

			var allNames = listBox.FindAll(TreeScope.Children, Condition.TrueCondition)
				.Cast<AutomationElement>()
				.Select(e => (string)e.GetCurrentPropertyValue(AutomationElement.NameProperty))
				.ToArray();
			Assert.IsTrue(allNames.Any(n => n.IndexOf("IsPortOpen.ps1", StringComparison.OrdinalIgnoreCase) >= 0),
				"IsPortOpen.ps1 item not found. Items: " + string.Join(" | ", allNames));
		}

		AutomationElement FindById(string id) =>
			_window.FindFirst(TreeScope.Descendants,
				new PropertyCondition(AutomationElement.AutomationIdProperty, id));

		static void SetValue(AutomationElement element, string text)
		{
			Assert.IsNotNull(element, "Element to set value on was null");
			((ValuePattern)element.GetCurrentPattern(ValuePattern.Pattern)).SetValue(text);
		}

		static T WaitFor<T>(Func<T> probe, TimeSpan timeout) where T : class
		{
			var deadline = DateTime.UtcNow + timeout;
			while (DateTime.UtcNow < deadline)
			{
				try { var r = probe(); if (r != null) return r; }
				catch { /* swallow and retry until deadline */ }
				Thread.Sleep(100);
			}
			return null;
		}
	}
}
