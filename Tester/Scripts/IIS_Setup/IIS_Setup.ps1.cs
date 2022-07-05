using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Xml;
using System.Xml.Serialization;

public class IIS_Setup
{

	public static Data GetSettings(string file)
	{
		return Deserialize<Data>(file);
	}

    public static T Deserialize<T>(string path)
	{
		if (!File.Exists(path))
			return default(T);
		var xml = File.ReadAllText(path);
		var sr = new StringReader(xml);
		var settings = new XmlReaderSettings();
		settings.DtdProcessing = DtdProcessing.Ignore;
		settings.XmlResolver = null;
		var reader = XmlReader.Create(sr, settings);
		var serializer = new XmlSerializer(typeof(T), new Type[] { typeof(T) });
		var o = (T)serializer.Deserialize(reader);
		reader.Dispose();
		return o;
	}

	public static void Serialize<T>(T o, string path)
	{
		var settings = new XmlWriterSettings();
		//settings.OmitXmlDeclaration = true;
		settings.Encoding = System.Text.Encoding.UTF8;
		settings.Indent = true;
		settings.IndentChars = "\t";
		var serializer = new XmlSerializer(typeof(T));
		// Serialize in memory first, so file will be locked for shorter times.
		var ms = new MemoryStream();
		var xw = XmlWriter.Create(ms, settings);
		serializer.Serialize(xw, o);
		File.WriteAllBytes(path, ms.ToArray());
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
		var xw = XmlTextWriter.Create(sb, xws);
		xd.WriteTo(xw);
		xw.Close();
		return sb.ToString();
	}

}

[XmlRoot("Data")]
public class Data
{

	public Data()
	{
		Prefix = "";
		Pools = new List<Item>();
		Sites = new List<Item>();
		Paths = new List<Item>();
	}

	public string Prefix  { get; set; }

	/// <Summary>
	/// If set then IIS Express.
	/// D:\Projects\Solution\Project\.vs\config\applicationhost.config
	/// </Summary>
	public string Config  { get; set; }

	[XmlArrayItem("Pool")]
	public List<Item> Pools { get; set; }

	[XmlArrayItem("Site")]
	public List<Item> Sites { get; set; }

	[XmlArrayItem("Path")]
	public List<Item> Paths { get; set; }
}

public class Item
{
	/// <summary>Name.</summary>
	[XmlAttribute] public string Name { get; set; }
	/// <summary>Path.</summary>
	[XmlAttribute] public string Path { get; set; }
}
