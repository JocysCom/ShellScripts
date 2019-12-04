using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public class Sync_Folders
{

	public static int ProcessArguments(string[] args)
	{
		//for (int i = 0; i < args.Length; i++)
		//	Console.WriteLine(string.Format("{0}. {1}", i, args[i]));
		// Requires System.Configuration.Installl reference.
		var ic = new InstallContext(null, args);
		var source = ic.Parameters["source"];
		var target = ic.Parameters["target"];
		var saveDates = ic.Parameters.ContainsKey("save_dates");
		var loadDates = ic.Parameters.ContainsKey("load_dates");
		Console.WriteLine("Source: {0}", source);
		Console.WriteLine("Target: {0}", target);
		Console.WriteLine("Folder: {0}", Environment.CurrentDirectory);
		// Get folders
		var sourceDI = new DirectoryInfo(source);
		var targetDI = new DirectoryInfo(target);
		// Get all files.
		var sourceFinder = new FileFinder();
		var sourceFiles = sourceFinder.GetFiles("*.*", true, sourceDI.FullName);
		var targetFinder = new FileFinder();
		var targetFiles = targetFinder.GetFiles("*.*", true, targetDI.FullName);
		// Get File Data.
		var sourceData = sourceFiles.Select(x => new FileData()
		{
			SourceFI = x,
			RelativePath = x.FullName.Substring(sourceDI.FullName.Length + 1),
			RelativePathUpper = x.FullName.Substring(sourceDI.FullName.Length + 1).ToUpperInvariant()
		}).ToList();
		var targetData = targetFiles.Select(x => new FileData()
		{
			TargetFI = x,
			RelativePath = x.FullName.Substring(targetDI.FullName.Length + 1),
			RelativePathUpper = x.FullName.Substring(targetDI.FullName.Length + 1).ToUpperInvariant(),
		}).ToList();
		// Map target files to source.
		for (int i = 0; i < sourceData.Count; i++)
		{
			var sourceItem = sourceData[i];
			var sourcePathUpper = sourceItem.RelativePathUpper;
			var targetItem = targetData.FirstOrDefault(x => x.RelativePathUpper == sourcePathUpper);
			if (targetItem != null)
			{
				sourceItem.TargetFI = targetItem.TargetFI;
				targetItem.Matched = true;
			}
		}
		Console.WriteLine();
		for (int i = 0; i < sourceData.Count; i++)
			Console.WriteLine("Source: {0}", sourceData[i].RelativePath);
		Console.WriteLine();
		for (int i = 0; i < targetData.Count; i++)
			Console.WriteLine("Target: {0}", targetData[i].RelativePath);
		Console.WriteLine();
		var targetDelete = targetData.Where(x => !x.Matched).ToList();
		for (int i = 0; i < targetDelete.Count; i++)
			Console.WriteLine("Delete: {0}", targetDelete[i].RelativePath);
		// Get reasons to sync.
		Console.WriteLine();
		for (int i = 0; i < sourceData.Count; i++)
		{
			var item = sourceData[i];
			item.SourceChecksum = GetHashFromFile(item.SourceFI.FullName);
			if (item.TargetFI == null)
			{
				item.Reason = SynReason.Missing;
				Console.WriteLine("{0,-8} {1}", item.Reason, item.RelativePath);
				continue;
			}
			item.TargetChecksum = GetHashFromFile(item.TargetFI.FullName);
			if (item.TargetFI.Length != item.SourceFI.Length)
			{
				item.Reason = SynReason.Size;
				Console.WriteLine("{0,-8} {1}", item.Reason, item.RelativePath);
				continue;
			}
			for (int c = 0; c < item.SourceChecksum.Length; c++)
			{
				if (item.SourceChecksum[c] != item.TargetChecksum[c])
				{
					item.Reason = SynReason.Checksum;
					break;
				}
			}
			if (item.Reason == SynReason.Checksum)
			{
				Console.WriteLine("{0,-8} {1}", item.Reason, item.RelativePath);
				var sc = string.Join("", item.SourceChecksum.Select(x => x.ToString("X2"))).ToLower();
				var tc = string.Join("", item.TargetChecksum.Select(x => x.ToString("X2"))).ToLower();
				Console.WriteLine("  Source: {0}", sc);
				Console.WriteLine("  Target: {0}", tc);
			}
		}
		if (saveDates)
		{
			SaveDates(sourceData, "Sync_Folders.dates.txt");
			SaveCheck(sourceData, "Sync_Folders.sha256");
		}
		return 0;
	}

	public static void SaveCheck(List<FileData> list, string path)
	{
		var sb = new StringBuilder();
		for (int i = 0; i < list.Count; i++)
		{
			var item = list[i];
			var sc = string.Join("", item.SourceChecksum.Select(x => x.ToString("X2"))).ToLower();
			sb.AppendFormat("{0} *{1}\r\n", sc, item.RelativePath);
		}
		File.WriteAllText(path, sb.ToString());
	}

	public static void SaveDates(List<FileData> list, string path)
	{
		var sb = new StringBuilder();
		for (int i = 0; i < list.Count; i++)
		{
			var item = list[i];
			sb.AppendFormat(
				"{0:yyyy-MM-ddTHH:mm:ss.fffffffzzz} {1:yyyy-MM-ddTHH:mm:ss.fffffffzzz} {2}\r\n",
				item.SourceFI.CreationTime, item.SourceFI.LastWriteTime, item.RelativePath
			);
		}
		File.WriteAllText(path, sb.ToString());
	}

	public enum SynReason
	{
		None,
		Missing,
		Size,
		Checksum,
	}


	public class FileData
	{
		public string RelativePath;
		public string RelativePathUpper;
		public FileInfo SourceFI;
		public FileInfo TargetFI;
		public byte[] SourceChecksum;
		public byte[] TargetChecksum;
		public SynReason Reason;
		public bool Matched;
	}

	#region Checksum

	public static byte[] GetHashFromFile(string path, object sender = null, ProgressChangedEventHandler progressHandler = null, RunWorkerCompletedEventHandler completedHandler = null)
	{
		var algorithm = System.Security.Cryptography.SHA256.Create();
		var hash = GetHashFromFile(algorithm, path, sender, progressHandler, completedHandler);
		algorithm.Dispose();
		return hash;
	}

	public static byte[] GetHashFromFile(HashAlgorithm algorithm, string path,
		object sender = null,
		ProgressChangedEventHandler progressHandler = null,
		RunWorkerCompletedEventHandler completedHandler = null
	)
	{
		// This method is equivalent to the FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read).
		// CWE-73: External Control of File Name or Path
		// Note: False Positive. File path is not externally controlled by the user.
		using (var stream = System.IO.File.OpenRead(path))
		{
			int _progress = -1;
			long totalBytes = stream.Length;
			long totalBytesRead = 0;
			// 4096 buffer preferable because the CPU cache can hold such amounts.
			var buffer = new byte[0x1000];
			bool done;
			int bytesRead;
			do
			{
				bytesRead = stream.Read(buffer, 0, buffer.Length);
				totalBytesRead += bytesRead;
				// True if reading of all bytes completed.
				done = totalBytesRead == totalBytes;
				// If more bytes left to read then...
				if (done)
					algorithm.TransformFinalBlock(buffer, 0, bytesRead);
				else
					algorithm.TransformBlock(buffer, 0, bytesRead, null, 0);
				var progress = (int)((double)totalBytesRead * 100 / totalBytes);
				var ev = progressHandler;
				if (_progress != progress && ev != null)
				{
					_progress = progress;
					ev(sender, new ProgressChangedEventArgs(progress, null));
				}
				// Continue if not done...
			} while (!done);
		}
		var hash = algorithm.Hash;
		var ev2 = completedHandler;
		if (ev2 != null)
			ev2(sender, new RunWorkerCompletedEventArgs(hash, null, false));
		return hash;
	}

	#endregion

	#region FileFinder Functions

	public class FileFinder
	{

		public event EventHandler<FileFinderEventArgs> FileFound;

		int _DirectoryIndex;
		List<DirectoryInfo> _Directories;
		public bool IsPaused { get; set; }

		public bool IsStopping { get; set; }

		public List<FileInfo> GetFiles(string searchPattern, bool allDirectories = false, params string[] paths)
		{
			IsStopping = false;
			IsPaused = false;
			var fis = new List<FileInfo>();
			_Directories = paths.Select(x => new DirectoryInfo(x)).ToList();
			for (int i = 0; i < _Directories.Count; i++)
			{
				// Pause or Stop.
				while (IsPaused && !IsStopping)
					// Logical delay without blocking the current thread.
					System.Threading.Tasks.Task.Delay(500).Wait();
				if (IsStopping)
					return fis;
				// Do tasks.
				_DirectoryIndex = i;
				var di = _Directories[i];
				// Skip folders if don't exists.
				if (!di.Exists) continue;
				AddFiles(di, ref fis, searchPattern, allDirectories);
			}
			return fis;
		}

		public void AddFiles(DirectoryInfo di, ref List<FileInfo> fileList, string searchPattern, bool allDirectories)
		{
			try
			{
				var patterns = searchPattern.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
				if (patterns.Length == 0)
				{
					// Lookup for all files.
					patterns = new[] { "" };
				}
				for (int p = 0; p < patterns.Length; p++)
				{
					var pattern = patterns[p];
					var files = string.IsNullOrEmpty(pattern)
						? di.GetFiles()
						: di.GetFiles(pattern);
					for (int i = 0; i < files.Length; i++)
					{
						// Pause or Stop.
						while (IsPaused && !IsStopping)
							// Logical delay without blocking the current thread.
							System.Threading.Tasks.Task.Delay(500).Wait();
						if (IsStopping)
							return;
						// Do tasks.
						var fullName = files[i].FullName;
						if (!fileList.Any(x => x.FullName == fullName))
						{
							fileList.Add(files[i]);
							var ev = FileFound;

							if (ev != null)
							{
								var e = new FileFinderEventArgs();
								e.Directories = _Directories;
								e.DirectoryIndex = _DirectoryIndex;
								e.FileIndex = fileList.Count - 1;
								e.Files = fileList;
								ev(this, e);
							}
						}
					}
				}
			}
			catch { }
			try
			{
				// If must search inside subdirectories then...
				if (allDirectories)
				{
					var subDis = di.GetDirectories();
					foreach (DirectoryInfo subDi in subDis)
					{
						// Pause or Stop.
						while (IsPaused && !IsStopping)
							// Logical delay without blocking the current thread.
							System.Threading.Tasks.Task.Delay(500).Wait();
						if (IsStopping)
							return;
						// Do tasks.
						AddFiles(subDi, ref fileList, searchPattern, allDirectories);
					}
				}
			}
			catch { }
		}

	}

	public class FileFinderEventArgs : EventArgs
	{
		public List<FileInfo> Files;
		public int FileIndex;
		public List<DirectoryInfo> Directories;
		public int DirectoryIndex;
	}

	#endregion

}
