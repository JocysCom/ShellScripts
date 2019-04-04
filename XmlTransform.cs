//=============================================================================
// Jocys.com XML Transform                    Evaldas Jocys <evaldas@jocys.com>
//=============================================================================
using Microsoft.Web.XmlTransform;
using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

public class Program
{
    public class Transform
    {
        public FileInfo Source;
        public string Type;
        public List<FileInfo> Transforms = new List<FileInfo>();
        public List<string> Environments = new List<string>();
    }

    public static void ProcessArguments(string[] args)
    {
        // IMPORTANT: Make sure this class don't have any static references to x360ce.Engine library or
        // program tries to load x360ce.Engine.dll before AssemblyResolve event is available and fails.
        AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
        //for (int i = 0; i < args.Length; i++)
        // Console.WriteLine(string.Format("{0}. {1}", i, args[i]));
        // Requires System.Configuration.Installl reference.
        var ic = new InstallContext(null, args);
        var script = ic.Parameters["s"];
        var environment = ic.Parameters["a"];
        var scriptFile = new FileInfo(script);
        var scriptName = System.IO.Path.GetFileNameWithoutExtension(scriptFile.Name);
        // Show parameters
        Console.Title = string.Format("{0} Script", scriptName);
        Console.WriteLine("Searching. Please wait...");
        var tranforms = GetTransforms(scriptFile.Directory.FullName);
        if (string.IsNullOrEmpty(environment))
        {
            var environments = tranforms
                .SelectMany(x => x.Environments)
                .Select(x => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(x.ToLower()))
                .Distinct().ToArray();
            // <action> <working_folder> <pattern> <data_file> <script_file_name>
            Console.WriteLine();
            Console.WriteLine("Transform Configuration Files:");
            Console.WriteLine("");
            for (int i = 0; i < environments.Length; i++)
            {
                Console.WriteLine(string.Format("    {0} - {1}", i, environments[i]));
            }
            Console.WriteLine();
            Console.Write("Type Number or press ENTER to exit: ");
            var key = Console.ReadKey(true);
            Console.WriteLine(string.Format("{0}", key.KeyChar));
            int n;
            if (!int.TryParse(key.KeyChar.ToString(), out n))
                return;
            if (n < 0 || n >= environments.Length)
                return;
            environment = environments[n];
        }
        TransformFolder(tranforms, environment);
    }

    public static Transform[] GetTransforms(string path)
    {
        var dir = new System.IO.DirectoryInfo(path);
        var appFiles = dir.GetFiles("App.Transform.Source.config", SearchOption.AllDirectories);
        var webFiles = dir.GetFiles("Web.Transform.Source.config", SearchOption.AllDirectories);
        var files = new List<FileInfo>();
        files.AddRange(appFiles);
        files.AddRange(webFiles);
        // Add default files.
        var dirs = files.Select(x => x.Directory.FullName).ToArray();
        var appConfigFiles = dir.GetFiles("App.config", SearchOption.AllDirectories);
        appConfigFiles = appConfigFiles.Where(x => !dirs.Contains(x.Directory.FullName)).ToArray();
        var webConfigFiles = dir.GetFiles("Web.config", SearchOption.AllDirectories);
        webConfigFiles = webConfigFiles.Where(x => !dirs.Contains(x.Directory.FullName)).ToArray();
        files.AddRange(appConfigFiles);
        files.AddRange(webConfigFiles);
        // Show menu.
        Console.WriteLine();
        Console.WriteLine("Path: {0}", path);
        var rx = new Regex("^(?<type>[^\\.]+)");
        // Put all list into files.
        var ts = files.Select(x => new Transform() { Source = x }).ToArray();
        for (int i = 0; i < ts.Length; i++)
        {
            var t = ts[i];
            // Get 'app' or 'web'.
            t.Type = rx.Match(t.Source.Name).Groups["type"].Value;
            var xmlTranformPattern = string.Format("^{0}\\.(?<env>[^\\.]+)\\.config$", t.Type);
            var xmlTranformRx = new Regex(xmlTranformPattern);
            // Get transform files.
            var xmlTransforms = t.Source.Directory.GetFiles(string.Format("{0}.*.config", t.Type));
            foreach (var xmlTransform in xmlTransforms)
            {
                var match = xmlTranformRx.Match(xmlTransform.Name);
                if (!match.Success)
                    continue;
                // Store transform file and environment.
                t.Transforms.Add(xmlTransform);
                t.Environments.Add(match.Groups["env"].Value);
            }
        }
        ts = ts.Where(x => x.Transforms.Count > 0).ToArray();
        Console.WriteLine("Files to Transform: {0}", ts.Length);
        return ts;
    }

    public static void TransformFolder(Transform[] transforms, string environment)
    {
        //Console.WriteLine(string.Format("Environment: {0}", environment));
        var maxName = transforms.SelectMany(x => x.Transforms.Select(y => y.Name.Length)).Max();
        var maxDest = 41;
        for (int i = 0; i < transforms.Length; i++)
        {
            var transform = transforms[i];
            Console.WriteLine();
            Console.WriteLine(string.Format("{0}", transform.Source.FullName));
            Console.WriteLine();
            var currentPath = string.Format("{0}\\{1}.config", transform.Source.Directory.FullName, transform.Type);
            var currentFile = new FileInfo(currentPath);
            if (currentFile.Exists)
            {
                var currentEnvironment = GetEnvironment(currentFile.FullName).ToLower();
                currentEnvironment = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(currentEnvironment);
                //Console.WriteLine(string.Format("    {0} ({1})", Path.GetFileName(currentPath), currentEnvironment.ToUpper()));
            }
            for (int t = 0; t < transform.Transforms.Count; t++)
            {
                var tFi = transform.Transforms[t];
                var env = transform.Environments[t];
                var transformPath = string.Format("{0}\\{1}.{2}.config", transform.Source.Directory.FullName, transform.Type, env);
                var destinationPath = string.Format("{0}\\{1}.Transform.Destination.{2}.config", transform.Source.Directory.FullName, transform.Type, env);
                var success = TransformConfig(transform.Source.FullName, transformPath, destinationPath, maxName, maxDest);
                // If success and match the choice.
                if (success && string.Equals(env, environment, StringComparison.OrdinalIgnoreCase))
                {
                    var bytes = File.ReadAllBytes(destinationPath);
                    if (IsDifferent(currentPath, bytes))
                    {
                        File.WriteAllBytes(currentPath, bytes);
                    }
                    Console.Write(" => {0}", Path.GetFileName(currentPath));
                }
                Console.WriteLine();
            }
        }
    }

    public static string GetEnvironment(string filename, string defaultValue = "Dev")
    {
        var xmlDoc = new XmlDocument();
        xmlDoc.Load(filename);
        foreach (XmlElement element in xmlDoc.DocumentElement)
        {
            if (!element.Name.Equals("appSettings"))
                continue;
            var elements = element.ChildNodes.OfType<System.Xml.XmlElement>();
            foreach (var el in elements)
            {
                if (el == null)
                    continue;
                if (el.Attributes["key"].Value == "RunMode" || el.Attributes["key"].Value == "Environment")
                {
                    return el.Attributes["value"].Value.ToLower();
                }
            }
        }
        return defaultValue;
    }

    // C:\Program Files(x86)\MSBuild\Microsoft\VisualStudio\v11.0\Web\Microsoft.Web.XmlTransform.dll
    public static bool TransformConfig(string sourcePath, string transformPath, string destinationPath, int maxName = 18, int maxDest = 28)
    {
        if (!File.Exists(sourcePath))
        {
            Console.WriteLine("Source file not found: {0}", sourcePath);
            return false;
        }
        if (!File.Exists(transformPath))
        {
            Console.WriteLine("Transform file not found: {0}", transformPath);
            return false;
        }
        var document = new XmlTransformableDocument();
        document.PreserveWhitespace = false;
		document.Load(sourcePath);
        var transformation = new XmlTransformation(transformPath);
        var status = transformation.Apply(document) ? "" : "Failure: ";
        Console.Write("    {0}{1,-" + maxName + "} => {2,-" + maxDest + "}", status, Path.GetFileName(transformPath), Path.GetFileName(destinationPath));
		var ms = new MemoryStream();
		var xws = new XmlWriterSettings();
		xws.Indent = true;
		xws.CheckCharacters = true;
		var xw = XmlTextWriter.Create(ms, xws);
		document.WriteTo(xw);
		xw.Close();
		//return sb.ToString();
		//document.Save(ms);
        var bytes = ms.ToArray();
        // If file is missing or different then...
        if (!File.Exists(destinationPath) || IsDifferent(destinationPath, bytes))
        {
            // Save file.
            document.Save(destinationPath);
        }
        document.Dispose();
        return true;
    }

    #region CurrentDomain_AssemblyResolve

    static List<string> LoadedAssemblies = new List<string>();

    static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs e)
    {
        var dllName = e.Name.Contains(",") ? e.Name.Substring(0, e.Name.IndexOf(',')) : e.Name.Replace(".dll", "");
        //Console.WriteLine(string.Format("AssemblyResolve [Name={0}]: {1}", e.Name, dllName));
        string path = null;
        switch (dllName)
        {
            case "Microsoft.Web.XmlTransform":
            case "Microsoft.Web.XmlTransform.resources":
                var editions = new string[] { "Community", "Professional", "Enterprise" };
                var versions = new List<KeyValuePair<string, string>>();
                versions.Add(new KeyValuePair<string, string>("2017", "15.0"));
                versions.Add(new KeyValuePair<string, string>("2019", "16.0"));
                foreach (var edition in editions)
                {
                    foreach (var version in versions)
                    {
                        var p = string.Format(@"c:\Program Files (x86)\Microsoft Visual Studio\{0}\{1}\MSBuild\Microsoft\VisualStudio\v{2}\Web\Microsoft.Web.XmlTransform.dll",
                            version.Key, edition, version.Value);
                        if (System.IO.File.Exists(p))
                            path = p;
                    }
                    if (!string.IsNullOrEmpty(path))
                        break;
                }
                break;
            default:
                break;
        }
        if (path == null)
        {
            Console.WriteLine(string.Format("AssemblyResolve: {0} - No Path!", dllName));
            return null;
        }
        if (LoadedAssemblies.Contains(path))
        {
            //Console.WriteLine(string.Format("AssemblyResolve: {0} - Already Loaded!", path));
            return null;
        }
        if (!System.IO.File.Exists(path))
        {
            Console.WriteLine(string.Format("AssemblyResolve: {0} - File not found!", path));
            return null;
        }
        var bytes = System.IO.File.ReadAllBytes(path);
        //Console.WriteLine(string.Format("AssemblyResolve Name: {0}", e.Name));
        //Console.WriteLine(string.Format("AssemblyResolve Loading: {0}", path));
        var asm = Assembly.Load(bytes);
        LoadedAssemblies.Add(path);
        return asm;
    }

    #endregion

    #region File Comparison

    public static bool IsDifferent(string name, byte[] bytes)
    {
        var fi = new FileInfo(name);
        var isDifferent = false;
        // If file doesn't exists or file size is different then...
        if (!fi.Exists || fi.Length != bytes.Length)
        {
            isDifferent = true;
        }
        else
        {
            // Compare checksums.
            var byteHash = GetHashFromBytes(bytes);
            var fileHash = GetHashFromFile(fi.FullName);
            for (int i = 0; i < byteHash.Length; i++)
                if (byteHash[i] != fileHash[i])
                    return false;
        }
        return isDifferent;
    }

    public static byte[] GetHashFromBytes(byte[] bytes)
    {
        var algorithm = System.Security.Cryptography.SHA256.Create();
        return algorithm.ComputeHash(bytes);
    }

    public static byte[] GetHashFromFile(string path, object sender = null)
    {
        var algorithm = System.Security.Cryptography.SHA256.Create();
        using (var stream = File.OpenRead(path))
        {
            var totalBytes = stream.Length;
            var totalBytesRead = 0L;
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
                // Continue if not done...
            } while (!done);
        }
        var hash = algorithm.Hash;
        algorithm.Dispose();
        return hash;
    }

    #endregion

}
