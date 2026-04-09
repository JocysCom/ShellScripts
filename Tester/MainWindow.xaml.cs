using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation.Language;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.Shell.Scripts.Tester
{
	/// <summary>
	/// Scans the configured Scripts folder and builds the UI dynamically.
	/// For each discovered script it lets the user fill in parameters and run it.
	/// PowerShell (.ps1) parameters are detected via the official PowerShell parser
	/// (System.Management.Automation.Language.Parser), so no regex guessing is needed.
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			DarkThemeCheckBox.IsChecked = JocysCom.Shell.Scripts.Tester.Properties.Settings.Default.DarkTheme;
			var configured = JocysCom.Shell.Scripts.Tester.Properties.Settings.Default.FolderTextBoxText;
			// Always prefer the product's own Tester\Scripts folder when it can be located
			// alongside the exe — the user explicitly wanted that to be the default.
			var bundled = GetDefaultScriptsFolder();
			FolderTextBox.Text = Directory.Exists(bundled)
				? bundled
				: (string.IsNullOrWhiteSpace(configured) || !Directory.Exists(configured) ? bundled : configured);
			LoadScripts();
		}

		static string GetDefaultScriptsFolder()
		{
			// Walk up from the executable looking for the canonical Tester\Scripts folder,
			// then fall back to any bare Scripts folder. This avoids resolving to a
			// drive root on first launch.
			var dir = new DirectoryInfo(AppContext.BaseDirectory);
			while (dir != null)
			{
				var testerScripts = Path.Combine(dir.FullName, "Tester", "Scripts");
				if (Directory.Exists(testerScripts))
					return testerScripts;
				var plainScripts = Path.Combine(dir.FullName, "Scripts");
				if (Directory.Exists(plainScripts))
					return plainScripts;
				dir = dir.Parent;
			}
			return AppContext.BaseDirectory;
		}

		void SaveSettings()
		{
			JocysCom.Shell.Scripts.Tester.Properties.Settings.Default.FolderTextBoxText = FolderTextBox.Text;
			JocysCom.Shell.Scripts.Tester.Properties.Settings.Default.DarkTheme = DarkThemeCheckBox.IsChecked == true;
			JocysCom.Shell.Scripts.Tester.Properties.Settings.Default.Save();
		}

		void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) => SaveSettings();

		void DarkThemeCheckBox_Changed(object sender, RoutedEventArgs e)
			=> App.ApplyTheme(DarkThemeCheckBox.IsChecked == true);

		void BrowseButton_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new System.Windows.Forms.FolderBrowserDialog
			{
				SelectedPath = Directory.Exists(FolderTextBox.Text) ? FolderTextBox.Text : AppContext.BaseDirectory,
				Description = "Select Scripts folder",
			};
			if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				FolderTextBox.Text = dialog.SelectedPath;
				LoadScripts();
			}
		}

		void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadScripts();

		void LoadScripts()
		{
			ScriptsListBox.ItemsSource = null;
			ParametersPanel.Children.Clear();
			ScriptHeader.Text = string.Empty;
			ScriptPathLabel.Text = string.Empty;
			RunButton.IsEnabled = false;
			OpenFolderButton.IsEnabled = false;

			var root = FolderTextBox.Text;
			if (!Directory.Exists(root))
			{
				SetStatus($"Folder not found: {root}");
				return;
			}

			var items = new List<ScriptItem>();
			// Skip ACL-denied folders silently — picking a drive root (e.g. C:\) would
			// otherwise crash on paths like C:\Config.Msi.
			var enumOptions = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false };
			IEnumerable<string> subDirs;
			try { subDirs = Directory.EnumerateDirectories(root, "*", enumOptions).OrderBy(x => x); }
			catch (Exception ex) { SetStatus($"Cannot read {root}: {ex.Message}"); return; }
			foreach (var subDir in subDirs)
			{
				var dirName = Path.GetFileName(subDir);
				IEnumerable<string> files;
				try { files = Directory.EnumerateFiles(subDir, "*", enumOptions).OrderBy(x => x); }
				catch { continue; }
				foreach (var file in files)
				{
					var ext = Path.GetExtension(file).ToLowerInvariant();
					if (ext != ".ps1" && ext != ".bat" && ext != ".cmd")
						continue;
					items.Add(new ScriptItem
					{
						DisplayName = $"{dirName}  —  {Path.GetFileName(file)}",
						FilePath = file,
						Kind = ext == ".ps1" ? ScriptKind.PowerShell : ScriptKind.Batch,
					});
				}
			}

			ScriptsListBox.ItemsSource = items;
			SetStatus($"Loaded {items.Count} script(s) from {root}");
		}

		ScriptItem _current;

		void ScriptsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			_current = ScriptsListBox.SelectedItem as ScriptItem;
			ParametersPanel.Children.Clear();
			if (_current == null)
			{
				RunButton.IsEnabled = false;
				OpenFolderButton.IsEnabled = false;
				ScriptHeader.Text = string.Empty;
				ScriptPathLabel.Text = string.Empty;
				return;
			}
			ScriptHeader.Text = Path.GetFileName(_current.FilePath);
			ScriptPathLabel.Text = _current.FilePath;
			RunButton.IsEnabled = true;
			OpenFolderButton.IsEnabled = true;

			if (_current.Kind == ScriptKind.PowerShell)
				BuildPowerShellParameterUI(_current);
			else
				BuildBatchParameterUI(_current);
		}

		void BuildPowerShellParameterUI(ScriptItem item)
		{
			List<ParameterAst> parameters;
			try
			{
				var ast = Parser.ParseFile(item.FilePath, out _, out var errors);
				if (errors != null && errors.Length > 0)
					SetStatus($"Parse warnings: {errors[0].Message}");
				parameters = ast.ParamBlock?.Parameters?.ToList()
					?? ast.FindAll(a => a is ParameterAst, false).OfType<ParameterAst>().ToList();
			}
			catch (Exception ex)
			{
				AddInfo($"Could not parse parameters: {ex.Message}");
				return;
			}

			item.ParameterInputs.Clear();
			if (parameters.Count == 0)
			{
				AddInfo("This script has no parameters.");
				return;
			}

			foreach (var p in parameters)
			{
				var name = p.Name.VariablePath.UserPath;
				var typeName = p.StaticType?.Name ?? "Object";
				var defaultValue = p.DefaultValue?.Extent?.Text ?? "";
				var isSwitch = string.Equals(typeName, "SwitchParameter", StringComparison.OrdinalIgnoreCase);
				var isBool = string.Equals(typeName, "Boolean", StringComparison.OrdinalIgnoreCase);

				var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
				row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
				row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

				var label = new TextBlock
				{
					Text = $"{name}  ({typeName})",
					VerticalAlignment = VerticalAlignment.Center,
					Margin = new Thickness(0, 0, 6, 0),
				};
				Grid.SetColumn(label, 0);
				row.Children.Add(label);

				FrameworkElement input;
				if (isSwitch || isBool)
				{
					var cb = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
					input = cb;
					item.ParameterInputs[name] = new ParameterInput(ParameterInputKind.Switch, cb);
				}
				else
				{
					var tb = new TextBox { Text = StripQuotes(defaultValue), VerticalContentAlignment = VerticalAlignment.Center };
					input = tb;
					item.ParameterInputs[name] = new ParameterInput(ParameterInputKind.Text, tb);
				}
				Grid.SetColumn(input, 1);
				row.Children.Add(input);
				ParametersPanel.Children.Add(row);
			}
		}

		void BuildBatchParameterUI(ScriptItem item)
		{
			item.ParameterInputs.Clear();
			AddInfo("Batch scripts use positional arguments. Provide them below (optional):");
			var tb = new TextBox { Margin = new Thickness(0, 2, 0, 2) };
			item.ParameterInputs["__args"] = new ParameterInput(ParameterInputKind.Text, tb);
			ParametersPanel.Children.Add(tb);
		}

		void AddInfo(string text)
			=> ParametersPanel.Children.Add(new TextBlock { Text = text, Opacity = 0.7, Margin = new Thickness(0, 2, 0, 2), TextWrapping = TextWrapping.Wrap });

		static string StripQuotes(string text)
		{
			if (string.IsNullOrEmpty(text)) return text;
			if (text.Length >= 2 && (text[0] == '"' || text[0] == '\'') && text[^1] == text[0])
				return text.Substring(1, text.Length - 2);
			return text;
		}

		void RunButton_Click(object sender, RoutedEventArgs e)
		{
			if (_current == null) return;
			try
			{
				var psi = _current.Kind == ScriptKind.PowerShell
					? BuildPowerShellStartInfo(_current)
					: BuildBatchStartInfo(_current);
				psi.WorkingDirectory = Path.GetDirectoryName(_current.FilePath) ?? "";
				psi.UseShellExecute = false;
				Process.Start(psi);
				SetStatus($"Started {Path.GetFileName(_current.FilePath)}");
			}
			catch (Exception ex)
			{
				SetStatus($"Failed: {ex.Message}");
				MessageBox.Show(this, ex.ToString(), "Run failed", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		static ProcessStartInfo BuildPowerShellStartInfo(ScriptItem item)
		{
			var args = new List<string> { "-NoExit", "-ExecutionPolicy", "Bypass", "-File", Quote(item.FilePath) };
			foreach (var kv in item.ParameterInputs)
			{
				if (kv.Value.Kind == ParameterInputKind.Switch)
				{
					if (((CheckBox)kv.Value.Control).IsChecked == true)
						args.Add("-" + kv.Key);
				}
				else
				{
					var text = ((TextBox)kv.Value.Control).Text;
					if (string.IsNullOrEmpty(text)) continue;
					args.Add("-" + kv.Key);
					args.Add(Quote(text));
				}
			}
			return new ProcessStartInfo(ResolvePowerShellExe(), string.Join(" ", args));
		}

		/// <summary>
		/// Prefer PowerShell 7+ (pwsh.exe). Searches PATH first, then the canonical
		/// install location, then falls back to Windows PowerShell 5.1.
		/// </summary>
		static string ResolvePowerShellExe()
		{
			foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
			{
				if (string.IsNullOrWhiteSpace(dir)) continue;
				var candidate = Path.Combine(dir, "pwsh.exe");
				if (File.Exists(candidate)) return candidate;
			}
			var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
			var standard = Path.Combine(pf, "PowerShell", "7", "pwsh.exe");
			if (File.Exists(standard)) return standard;
			return "powershell.exe";
		}

		static ProcessStartInfo BuildBatchStartInfo(ScriptItem item)
		{
			var tail = "";
			if (item.ParameterInputs.TryGetValue("__args", out var pi))
				tail = ((TextBox)pi.Control).Text ?? "";
			return new ProcessStartInfo("cmd.exe", $"/k \"\"{item.FilePath}\" {tail}\"");
		}

		static string Quote(string s) => s.Contains(' ') ? "\"" + s + "\"" : s;

		void OpenFolderButton_Click(object sender, RoutedEventArgs e)
		{
			if (_current == null) return;
			var dir = Path.GetDirectoryName(_current.FilePath);
			if (!string.IsNullOrEmpty(dir))
				Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
		}

		void SetStatus(string text)
			=> StatusLabel.Text = $"{DateTime.Now:HH:mm:ss}  {text}";

		// ---- model types ----

		enum ScriptKind { PowerShell, Batch }

		enum ParameterInputKind { Text, Switch }

		class ScriptItem
		{
			public string DisplayName { get; set; }
			public string FilePath { get; set; }
			public ScriptKind Kind { get; set; }
			public Dictionary<string, ParameterInput> ParameterInputs { get; } = new Dictionary<string, ParameterInput>();
			// ListBox exposes this string as the item's UIA Name property.
			public override string ToString() => DisplayName;
		}

		class ParameterInput
		{
			public ParameterInput(ParameterInputKind kind, FrameworkElement control) { Kind = kind; Control = control; }
			public ParameterInputKind Kind { get; }
			public FrameworkElement Control { get; }
		}
	}
}
