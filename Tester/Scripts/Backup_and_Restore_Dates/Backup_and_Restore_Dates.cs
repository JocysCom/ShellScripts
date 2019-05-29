using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Configuration;
using System.Configuration.Install;

public class Backup_and_Restore_Dates
{

	public static void ProcessArguments(string[] args)
	{
		//for (int i = 0; i < args.Length; i++)
		// Console.WriteLine(string.Format("{0}. {1}", i, args[i]));
		// Requires System.Configuration.Installl reference.
		var ic = new InstallContext(null, args);
		var script = ic.Parameters["s"];
		var action = ic.Parameters["a"];
		var folder = ic.Parameters["f"];
		var pattern = ic.Parameters["p"];
		var dataFile = ic.Parameters["d"];
		var scriptFile = new FileInfo(script);
		var scriptName = System.IO.Path.GetFileNameWithoutExtension(scriptFile.Name);
		// Show parameters
		Console.WriteLine(string.Format("Working Folder: {0}", folder));
		Console.WriteLine(string.Format("Search Pattern: {0}", pattern));
		Console.WriteLine(string.Format("Data File:      {0}", dataFile));
		Console.WriteLine(string.Format("Script Name:    {0}", scriptName));
		Console.WriteLine();
		if (string.IsNullOrEmpty(action))
		{
			// <action> <working_folder> <pattern> <data_file> <script_file_name>
			Console.WriteLine();
			Console.WriteLine("Backup or Restore Directory and File dates.");
			Console.WriteLine("");
			Console.WriteLine("    1 - Backup Dates");
			Console.WriteLine("    2 - Restore Dates");
			Console.WriteLine();
			Console.Write("Type Number or press ENTER to exit: ");
			var key = Console.ReadKey(true);
			Console.WriteLine(string.Format("{0}", key.KeyChar));
			if (key.KeyChar == '1')
				action = "backup";
			if (key.KeyChar == '2')
				action = "restore";
		}
		var currentFolder = new System.IO.DirectoryInfo(folder);
		var infos = currentFolder.GetFileSystemInfos(pattern, System.IO.SearchOption.AllDirectories);
		if (action == "backup")
		{
			var sb = new StringBuilder();
			var lines = new List<Line>();
			foreach (var info in infos)
			{
				var line = new Line();
				line.IsDirectory = File.GetAttributes(info.FullName).HasFlag(FileAttributes.Directory);
				if (!line.IsDirectory)
					line.Length = new FileInfo(info.FullName).Length;
				// Get path relative to current folder.
				line.Created = info.CreationTime;
				line.Modified = info.LastWriteTime;
				line.Path = info.FullName.Substring(currentFolder.FullName.Length);
				// Skip some files.
				if (info.Name == scriptName + ".cs" || info.Name == scriptName + ".bat" || info.Name == dataFile)
					continue;
				lines.Add(line);
				Console.Write(".");
			}
			var maxLength = lines.Max(x => (x.IsDirectory ? "<DIR>" : x.Length.ToString()).Length);
			foreach (var line in lines)
			{
				// Add line.
				sb.AppendFormat(
				"{0:yyyy-MM-ddTHH:mm:ss.fffffffzzz} {1:yyyy-MM-ddTHH:mm:ss.fffffffzzz} {2," + maxLength + "} {3}\r\n",
				line.Created, line.Modified, line.IsDirectory ? "<DIR>" : line.Length.ToString(), line.Path);
			}
			System.IO.File.WriteAllText(dataFile, sb.ToString());
		}
		else if (action == "restore")
		{
			var lines = File.ReadLines(dataFile);
			foreach (var line in lines)
			{
				var l = new Line(line);
				if (string.IsNullOrEmpty(l.Path))
				{
					Console.WriteLine(string.Format("Is empty: '{0}'", line));
					continue;
				}
				var path = Path.Combine(currentFolder.FullName, l.Path);
				if (!File.Exists(path) && !Directory.Exists(path))
				{
					Console.WriteLine(string.Format("Not exist: '{0}'", line));
					continue;
				}
				var isDirectory = File.GetAttributes(path).HasFlag(FileAttributes.Directory);
				var fsi = isDirectory
					? (FileSystemInfo)new DirectoryInfo(path)
					: (FileSystemInfo)new FileInfo(path);
				var length = isDirectory ? 0L : ((FileInfo)fsi).Length;
				// Skip if size do not match
				if (length != l.Length)
				{
					Console.WriteLine(string.Format("Wrong size: '{0}'", line));
					continue;
				}
				try
				{
					// Fix Last Create time.
					if (l.Created > l.Modified)
						l.Created = l.Modified;
					// Restore Last Create time.
					if (fsi.CreationTime != l.Created)
						fsi.CreationTime = l.Created;
					// Restore Last Write Time.
					if (fsi.LastWriteTime != l.Modified)
						fsi.LastWriteTime = l.Modified;
					Console.Write(".");
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
			}
		}
		Console.WriteLine();
	}

	#region Help Functions and Classes

	static Regex rx = new Regex(@"(?<Created>[^\s]+)\s+(?<Modified>[^\s]+)\s+(?<Length>[^\s]+)\s+(?<Path>.*)");

	public class Line
	{
		public Line(string s = null)
		{
			if (string.IsNullOrEmpty(s))
				return;
			var match = rx.Match(s);
			if (!match.Success)
			{
				Console.WriteLine(string.Format("No match: '{0}'", s));
				return;
			}
			Created = DateTime.Parse(match.Groups["Created"].Value);
			Modified = DateTime.Parse(match.Groups["Modified"].Value);
			long length;
			IsDirectory = !long.TryParse(match.Groups["Length"].Value, out length);
			Length = length;
			Path = match.Groups["Path"].Value;
		}
		public DateTime Created { get; set; }
		public DateTime Modified { get; set; }
		public bool IsDirectory { get; set; }
		public long Length { get; set; }
		public string Path { get; set; }
	}

	#endregion

}

