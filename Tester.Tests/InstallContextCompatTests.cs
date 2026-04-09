// @under-test: Tester/JocysCom/InstallContextCompat.cs
// @area: args   @layer: unit
using System.Configuration.Install;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JocysCom.Shell.Scripts.Tester.Tests
{
	[TestClass]
	public class InstallContextCompatTests
	{
		[TestMethod]
		public void Parses_slash_key_equals_value()
		{
			var ic = new InstallContext(null, new[] { "/Computer=host", "/Port=443" });
			Assert.AreEqual("host", ic.Parameters["Computer"]);
			Assert.AreEqual("443", ic.Parameters["Port"]);
		}

		[TestMethod]
		public void Parses_dash_key_equals_value()
		{
			var ic = new InstallContext(null, new[] { "-name=alice" });
			Assert.AreEqual("alice", ic.Parameters["name"]);
		}

		[TestMethod]
		public void Bare_switch_is_empty_string()
		{
			var ic = new InstallContext(null, new[] { "/verbose" });
			Assert.IsTrue(ic.Parameters.ContainsKey("verbose"));
			Assert.AreEqual("", ic.Parameters["verbose"]);
		}

		[TestMethod]
		public void Null_args_yields_empty_parameters()
		{
			var ic = new InstallContext(null, null);
			Assert.AreEqual(0, ic.Parameters.Count);
		}
	}
}
