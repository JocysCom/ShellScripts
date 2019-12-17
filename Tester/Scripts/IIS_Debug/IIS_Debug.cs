using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public class IIS_Debug
{

	public static int ProcessArguments(string[] args)
	{
		Console.Title = string.Format("IIS {0} computer.", Environment.MachineName);
		var ic = new InstallContext(null, args);
		string exe;
		//exe = "%ProgramFiles(x86)%\IIS Express\appcmd.exe";
		exe = @"%systemroot%\system32\inetsrv\appcmd.exe";
		exe = Environment.ExpandEnvironmentVariables(exe);
		//LIST APPPOOLS
		//LIST SITE
		//LIST APP
		//LIST WP
		var outBuilder = new StringBuilder();
		Execute(exe, "LIST APPPOOLS", outBuilder);
		//Execute(exe, "LIST SITE", outBuilder);
		//Execute(exe, "LIST APP", outBuilder);
		Execute(exe, "LIST WP", outBuilder);
		//Console.WriteLine("Output:");
		//Console.Write(outBuilder.ToString());

		var menu = new Dictionary<string, string>();
		var procs = new Dictionary<string, Process>();
		var apps = new Dictionary<string, string>();

		var rx = new Regex(@"WP\s*""(?<id>[0-9]+)""\s*\(applicationPool:\s*(?<name>[^)]+)\)");
		var output = outBuilder.ToString();
		var matches = rx.Matches(output);
		for (int i = 0; i < matches.Count; i++)
		{
			var m = matches[i];
			var processId = int.Parse(m.Groups["id"].Value);
			var appName = m.Groups["name"].Value;
			var p = System.Diagnostics.Process.GetProcessById(processId);
			var description = string.Format(
				"Process: ID={0,-6}, Name={1}, Platform={2}, Pool={3}",
				processId, p.ProcessName, Is64Bit(p) ? "32-bit" : "64-bit", appName
			);
			menu.Add(i.ToString(), description);
			procs.Add(i.ToString(), p);
			apps.Add(i.ToString(), appName);
		}
		Console.WriteLine();
		Console.WriteLine("Application:");
		Console.WriteLine();
		foreach (var key in menu.Keys)
		{
			Console.WriteLine("    {0} - {1}", key, menu[key]);
		}
		Console.WriteLine();
		Console.Write("Type Number or press ENTER to exit: ");
		var k = Console.ReadKey(true);
		Console.WriteLine(k.KeyChar);
		Console.WriteLine();
		var choice = k.KeyChar.ToString();
		if (menu.Keys.Contains(choice))
		{
			using (var errorsWaitHandle = new AutoResetEvent(false))
			{
				var p = procs[choice];
				// Collect debug data.
				var DataCollector = new DebugDataCollector();
				DataCollector.CollectionFinished += (sender, e) =>
				{
					// Allow console to continue.
					errorsWaitHandle.Set();
				};
				// Begin collect data.
				DataCollector.CollectData(p, true, true, apps[choice]);
				// Wait till release signal will come.
				errorsWaitHandle.WaitOne();
			}
		}
		//var taskFile = (ic.Parameters["TaskFile"] ?? "").Replace("\"", "");
		//var computer = (ic.Parameters["Computer"] ?? "").Replace("\"", "");
		//var protocol = (ic.Parameters["Protocol"] ?? "").Replace("\"", "");
		return 0;
	}

	#region Helper Methods

	/// <summary>
	/// Execute console command.
	/// </summary>
	/// <param name="fileName">Console application to execute.</param>
	/// <param name="arguments">Arguments.</param>
	/// <param name="output">String builder for output.</param>
	/// <param name="error">String builder for errors. If null then output will be used.</param>
	/// <param name="timeout">Timeout. Default is -1, which represents an infinite time-out.</param>
	/// <returns>Exit code. Return null if timeout.</returns>
	public static int? Execute(string fileName, string arguments, StringBuilder outBuilder, StringBuilder errBuilder = null, int timeout = -1)
	{
		int? exitCode = null;
		var si = new ProcessStartInfo();
		// Do not use the OS shell.
		si.UseShellExecute = false;
		// Allow writing output to the standard output.
		si.RedirectStandardOutput = true;
		// Allow writing error to the standard error.
		si.RedirectStandardError = true;
		// Hide window.
		si.CreateNoWindow = true;
		si.FileName = fileName;
		si.Arguments = arguments;
		if (errBuilder == null)
			errBuilder = outBuilder;
		using (var outputWaitHandle = new AutoResetEvent(false))
		{
			using (var errorsWaitHandle = new AutoResetEvent(false))
			{
				using (var p = new Process() { StartInfo = si })
				{
					var receiveLock = new object();
					//var receiveLine = 0;
					var receiveSkip = false;
					var action = new Action<AutoResetEvent, DataReceivedEventArgs, StringBuilder>((ev, e, sb) =>
					{
						// If redirected stream is closed (a null line is sent) then...
						if (e.Data == null)
							// Allow WaitOne line to proceed.
							ev.Set();
						// If double empty line then skip...
						else if (e.Data.Length == 0 && receiveSkip) { receiveSkip = false; }
						else
							lock (receiveLock)
							{
								//sb.AppendFormat("{0}. {1}\r\n", ++receiveLine, e.Data);
								sb.AppendLine(e.Data);
								// Workaround: Allow to skip next empty line.
								receiveSkip = true;
							}
					});
					// Inside event call function with correct handler and string builder.
					var outputReceived = new DataReceivedEventHandler((sender, e) => action(outputWaitHandle, e, outBuilder));
					var errorsReceived = new DataReceivedEventHandler((sender, e) => action(errorsWaitHandle, e, errBuilder));
					p.OutputDataReceived += outputReceived;
					p.ErrorDataReceived += errorsReceived;
					p.Start();
					p.BeginErrorReadLine();
					p.BeginOutputReadLine();
					if (p.WaitForExit(timeout))
						// Process completed. Check process.ExitCode here.
						exitCode = p.ExitCode;
					p.Close();
					// Detach events before disposing process.
					// If timeout is set and is too small then events will be detached before all data received.
					p.OutputDataReceived -= outputReceived;
					p.ErrorDataReceived -= errorsReceived;
				}
				errorsWaitHandle.WaitOne(timeout);
			}
			// Timeout handlers after 'p' is disposed to make sure that handers are not used in events.
			outputWaitHandle.WaitOne(timeout);
		}
		return exitCode;
	}

	public static bool Is64Bit(Process process)
	{
		if (!Environment.Is64BitOperatingSystem)
			return false;
		// if this method is not available in your version of .NET, use GetNativeSystemInfo via P/Invoke instead

		bool isWow64;
		if (!IsWow64Process(process.Handle, out isWow64))
			throw new Win32Exception();
		return !isWow64;
	}

	[DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsWow64Process([In] IntPtr process, [Out] out bool wow64Process);

	#endregion

	#region DataCollector

	public class DebugDataCollector : IDisposable
	{

		public DebugDataCollector()
		{
			LogsDir = new DirectoryInfo("Logs");
			if (!LogsDir.Exists)
				LogsDir.Create();
			ThreadMonitor = new System.Timers.Timer();
			ThreadMonitor.AutoReset = false;
			ThreadMonitor.Interval = 1000;
			ThreadMonitor.Elapsed += ThreadMonitor_Elapsed;
		}

		object ThreadListLock = new object();

		public event EventHandler<EventArgs> CollectionFinished;

		private void ThreadMonitor_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			lock (ThreadListLock)
			{
				var dumpIsComplete = _CollectDump && DumpTask != null && DumpTask.IsCompleted;
				var attaIsComplete = _CollectPerf && AttachTask != null && AttachTask.IsCompleted;
				var detaIsComplete = _CollectPerf && DetachTask != null && DetachTask.IsCompleted;
				var dumpDone = !_CollectDump || (_CollectDump && dumpIsComplete);
				var perfDone = !_CollectPerf || (_CollectPerf && detaIsComplete);
				try
				{
					// If must collect dump but not started then...
					if (_CollectDump && DumpTask == null)
						DumpTask = StartDump(_Process);
					// If must collect performance but not started then...
					if (_CollectPerf && AttachTask == null && dumpDone)
					{
						AttachTask = StartAttach(_Process);
						Console.WriteLine("Detach in {0} seconds {1:HH:mm:ss}.", _PerformanceSeconds, AttachTaskTime.AddSeconds(_PerformanceSeconds));
					}
					if (AttachTask != null && !AttachTask.IsCompleted)
					{
						//var status = StartStatus().Result;
					}
					// If task was attached for specified seconds then...
					if (_CollectPerf && DetachTask == null && AttachTask != null && DateTime.Now.Subtract(AttachTaskTime).TotalSeconds > _PerformanceSeconds)
					{
						DetachTask = StartDetach();
					}
					if (dumpDone && perfDone)
					{
						Console.WriteLine("Debug Data collection finished.");
						var ev = CollectionFinished;
						if (ev != null)
							ev(this, new EventArgs());
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.ToString());
				}
				if ((!dumpDone || !perfDone) && !IsDisposing)
				{
					var timer = (System.Timers.Timer)sender;
					timer.Start();
				}
			}
		}

		DirectoryInfo LogsDir;
		System.Timers.Timer ThreadMonitor;

		public bool CollectData(int processId)
		{
			var process = Process.GetProcessById(processId);
			return CollectData(process);
		}

		public bool CollectData(string processName)
		{
			var process = Process.GetProcessesByName(processName).FirstOrDefault();
			return CollectData(process);
		}

		Process _Process;
		bool _CollectDump;
		bool _CollectPerf;
		string _AppName;
		int _PerformanceSeconds;

		public bool CollectData(Process process, bool collectDump = true, bool collectPerformace = true, string appName = null, int perfomanceSeconds = 30)
		{
			lock (ThreadListLock)
			{
				if (process != null)
				{
					_Process = process;
					_CollectDump = collectDump;
					_CollectPerf = collectPerformace;
					_AppName = appName;
					_PerformanceSeconds = perfomanceSeconds;
					ThreadMonitor.Start();
				}
			}
			return process != null;
		}


		DateTime DumpTaskTime;
		Task<int?> DumpTask;
		DateTime AttachTaskTime;
		Task<int?> AttachTask;
		DateTime DetachTaskTime;
		Task<int?> DetachTask;

		/// <summary>
		/// Dump profiler to executable.
		/// </summary>
		Task<int?> StartDump(Process process)
		{
			DumpTaskTime = DateTime.Now;
			var logPrefix = string.Format("Log_{0:yyyyMMdd_HHmmss.ffffff}{1}", DumpTaskTime, ".Dump");
			// -ma          Write a 'Full' dump file.
			// -e           Write a dump when the process encounters an unhandled exception.
			//              Include the 1 to create dump on first chance exceptions.
			// - accepteula Automatically accept the Sysinternals license agreement.
			var arguments = string.Format("-accepteula -ma {0} \"Logs\\{1}.{0}.dmp\"", process.Id, logPrefix);
			return CreateTask(".Dump", @"Tools\ProcDump.exe", arguments);
		}

		/// <summary>Attach profiler to executable.</summary>
		Task<int?> StartAttach(Process process)
		{
			AttachTaskTime = DateTime.Now;
			var logPrefix = string.Format("Log_{0:yyyyMMdd_HHmmss.ffffff}{1}", AttachTaskTime, ".Attach");
			var arguments = string.Format(@"/attach:{0} /file:Logs\{1}", process.Id, logPrefix);
			return CreateTask(".Attach", @"Tools\VSPerf\VSPerf.exe", arguments);
		}

		/// <summary>Get Profiles status.</summary>
		Task<int?> StartStatus()
		{
			return CreateTask(".Status", @"Tools\VSPerf\VSPerf.exe", "/status");
		}

		/// <summary>Detach profiler from executable.</summary>
		Task<int?> StartDetach()
		{
			DetachTaskTime = DateTime.Now;
			return CreateTask(".Detach", @"Tools\VSPerf\VSPerf.exe", "/detach");
		}

		Task<int?> CreateTask(string prefix, string fileName, string arguments)
		{
			var fi = new FileInfo(fileName);
			var task = Task.Factory.StartNew(() =>
			{
				Console.WriteLine();
				Console.WriteLine("{0} Start", prefix);
				var outBuilder = new StringBuilder();
				var results = Execute(fi.FullName, arguments, outBuilder);
				Console.WriteLine("{0}", IdentText(4, outBuilder.ToString(), ' '));
				Console.WriteLine("{0} End: {1}", prefix, results);
				return results;
			}, TaskCreationOptions.LongRunning);
			return task;
		}

		public static string IdentText(int tabs, string s, char ident = '\t')
		{
			if (tabs == 0)
				return s;
			if (s == null)
				s = string.Empty;
			var sb = new StringBuilder();
			var tr = new StringReader(s);
			var prefix = string.Empty;
			for (var i = 0; i < tabs; i++) prefix += ident;
			string line;
			while ((line = tr.ReadLine()) != null)
			{
				if (sb.Length > 0)
					sb.AppendLine();
				if (tabs > 0)
					sb.Append(prefix);
				sb.Append(line);
			}
			tr.Dispose();
			return sb.ToString();
		}


		#region IDisposable

		// Dispose() calls Dispose(true)
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		bool IsDisposing;

		// The bulk of the clean-up code is implemented in Dispose(bool)
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				IsDisposing = true;
				if (ThreadMonitor != null)
				{
					ThreadMonitor.Dispose();
					ThreadMonitor = null;
				}
			}
		}

		#endregion


	}

	#endregion
}