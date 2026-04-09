#if NET
using System.Collections.Generic;
using System.Collections.Specialized;

namespace System.Configuration.Install
{
	/// <summary>
	/// Minimal polyfill for System.Configuration.Install.InstallContext used by the legacy
	/// shell script helpers to parse command-line arguments of the form /key=value or /key.
	/// System.Configuration.Install is not available on modern .NET, so this shim reproduces
	/// only the members the scripts rely on: the Parameters indexer.
	/// </summary>
	public class InstallContext
	{
		public InstallContext() : this(null, null) { }

		public InstallContext(string logFilePath, string[] commandLine)
		{
			Parameters = ParseCommandLine(commandLine);
		}

		public StringDictionary Parameters { get; }

		public static StringDictionary ParseCommandLine(string[] args)
		{
			var result = new StringDictionary();
			if (args == null)
				return result;
			foreach (var raw in args)
			{
				if (string.IsNullOrEmpty(raw))
					continue;
				var arg = raw;
				if (arg[0] == '/' || arg[0] == '-')
					arg = arg.Substring(1);
				var eq = arg.IndexOf('=');
				string key, value;
				if (eq < 0)
				{
					key = arg;
					value = string.Empty;
				}
				else
				{
					key = arg.Substring(0, eq);
					value = arg.Substring(eq + 1);
				}
				result[key] = value;
			}
			return result;
		}
	}
}
#endif
