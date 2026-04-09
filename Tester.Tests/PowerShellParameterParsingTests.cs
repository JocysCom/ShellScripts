// @under-test: Tester/MainWindow.xaml.cs
// @area: script-discovery   @layer: unit
using System.Linq;
using System.Management.Automation.Language;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JocysCom.Shell.Scripts.Tester.Tests
{
	[TestClass]
	public class PowerShellParameterParsingTests
	{
		[TestMethod]
		public void IsPortOpen_ps1_exposes_expected_parameters()
		{
			var ast = Parser.ParseFile(TestPaths.IsPortOpenPs1, out _, out var errors);
			Assert.IsTrue(errors == null || errors.Length == 0,
				"Parser errors: " + (errors == null ? "" : string.Join("; ", errors.Select(e => e.Message))));

			var names = ast.ParamBlock?.Parameters
				.Select(p => p.Name.VariablePath.UserPath)
				.OrderBy(n => n)
				.ToArray();
			CollectionAssert.AreEqual(
				new[] { "Computer", "Port", "Quiet", "TimeoutMs" },
				names);

			var quiet = ast.ParamBlock!.Parameters.Single(p => p.Name.VariablePath.UserPath == "Quiet");
			Assert.AreEqual("SwitchParameter", quiet.StaticType?.Name);

			var port = ast.ParamBlock.Parameters.Single(p => p.Name.VariablePath.UserPath == "Port");
			Assert.AreEqual("Int32", port.StaticType?.Name);
		}
	}
}
