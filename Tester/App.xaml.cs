using System;
using System.Windows;

namespace JocysCom.Shell.Scripts.Tester
{
	public partial class App : Application
	{
		public const string LightThemeUri = "/JocysCom/Controls/Themes/Default.xaml";
		public const string DarkThemeUri = "/JocysCom/Controls/Themes/Default_DarkTheme.xaml";

		public static bool IsDarkTheme { get; private set; }

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			ApplyTheme(JocysCom.Shell.Scripts.Tester.Properties.Settings.Default.DarkTheme);
		}

		/// <summary>Swap the merged theme dictionaries so every control re-styles instantly.</summary>
		public static void ApplyTheme(bool dark)
		{
			IsDarkTheme = dark;
			var dicts = Current.Resources.MergedDictionaries;
			dicts.Clear();
			dicts.Add(new ResourceDictionary { Source = new Uri(LightThemeUri, UriKind.Relative) });
			if (dark)
				dicts.Add(new ResourceDictionary { Source = new Uri(DarkThemeUri, UriKind.Relative) });
		}
	}
}
