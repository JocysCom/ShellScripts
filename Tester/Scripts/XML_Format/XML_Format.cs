using System;
using System.Configuration.Install;
using System.Net;
using System.Linq;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Text;
using System.IO;
using System.Collections.Generic;

public class Program
{

	public static void Main(string[] args)
	{
		//for (int i = 0; i < args.Length; i++)
		//	Console.WriteLine(string.Format("{0}. {1}", i, args[i]));
		// Requires System.Configuration.Installl reference.
		var ic = new InstallContext(null, args);
		var path = ic.Parameters["path"];
		var di = new DirectoryInfo(path);
		if (!di.Exists)
			return;
		Console.SetWindowSize(Math.Min(Console.LargestWindowWidth, 100), Math.Min(Console.LargestWindowHeight, 24));
		var files = new List<FileInfo>();
		files.AddRange(di.GetFiles("*.config", SearchOption.TopDirectoryOnly));
		files.AddRange(di.GetFiles("*.xml", SearchOption.TopDirectoryOnly));
		var letters = "0123456789abcdefghijklmnopqrstuvwxyz";
		Console.WriteLine("XML Files:");
		Console.WriteLine("");
		var min = Math.Min(files.Count, letters.Length);
		for (int i = 0; i < min; i++)
		{
			Console.WriteLine(string.Format("    {0} - {1}", letters[i], files[i].Name));
		}
		Console.WriteLine();
		Console.Write("Choose or press ENTER to exit: ");
		var key = Console.ReadKey(true);
		var s = key.KeyChar.ToString();
		Console.WriteLine(string.Format("{0}", key.KeyChar));
		if (string.IsNullOrEmpty(s))
			return;
		var index = letters.IndexOf(s);
		if (index < 0 || index >= min)
			return;
		var file = files[index];
		Console.Write("Format: {0}", file.Name);
		Console.WriteLine();
		var xml = File.ReadAllText(file.FullName);
		xml = XmlFormat(xml);
		File.WriteAllText(file.FullName, xml);
	}

	/// <summary>
	/// Reformat XML document.
	/// </summary>
	/// <param name="xml"></param>
	/// <returns></returns>
	public static string XmlFormat(string xml)
	{
		var xd = new XmlDocument();
		xd.XmlResolver = null;
		xd.LoadXml(xml);
		var sb = new StringBuilder();
		var xws = new XmlWriterSettings();
		xws.Indent = true;
		xws.CheckCharacters = true;
		xws.IndentChars = "\t";
		//xws.NewLineOnAttributes = true;
		var xw = XmlTextWriter.Create(sb, xws);
		xd.WriteTo(xw);
		xw.Close();
		return sb.ToString();
	}
	

}

