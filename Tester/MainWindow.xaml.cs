using System;
using System.IO;
using System.Windows;

namespace JocysCom.XmlTransform.Tester
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			LoadSettings();
		}

		private void FolderButton_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new System.Windows.Forms.FolderBrowserDialog();
			dialog.SelectedPath = FolderTextBox.Text;
			var result = dialog.ShowDialog();
			if (result == System.Windows.Forms.DialogResult.OK)
			{
				FolderTextBox.Text = dialog.SelectedPath;
			}
		}

		void LoadSettings()
		{
			FolderTextBox.Text = Properties.Settings.Default.FolderTextBoxText;
			EnvironmentTextBox.Text = Properties.Settings.Default.EnvironmentTextBoxText;
		}

		void SaveSettings()
		{
			Properties.Settings.Default.FolderTextBoxText = FolderTextBox.Text;
			Properties.Settings.Default.EnvironmentTextBoxText = EnvironmentTextBox.Text;
			Properties.Settings.Default.Save();
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			SaveSettings();
		}

		void Prepare<T>()
		{
			var location = System.Reflection.Assembly.GetExecutingAssembly().Location;
			var fi = new System.IO.FileInfo(location);
			var dir = fi.Directory.Parent.Parent;
			var path = Path.Combine(dir.FullName, "Scripts", typeof(T).Name);
			System.IO.Directory.SetCurrentDirectory(path);
			JocysCom.ClassLibrary.Runtime.ConsoleNativeMethods.CreateConsole();
		}

		private void TransformButton_Click(object sender, RoutedEventArgs e)
		{
			var transforms = XML_Transform.GetTransforms(FolderTextBox.Text);
			XML_Transform.TransformFolder(transforms, EnvironmentTextBox.Text);
			StatusLabel.Content = string.Format("{0:yyyy-MM-dd HH:mm:ss}: Done", DateTime.Now);
		}

		private void ConfigFilesReportButton_Click(object sender, RoutedEventArgs e)
		{
			Prepare<Config_Files_Report>();
			var result = Config_Files_Report.ProcessArguments(null);
		}

		private void ListDomainComputersButton_Click(object sender, RoutedEventArgs e)
		{
			Prepare<List_Domain_Computers>();
			var result = List_Domain_Computers.ProcessArguments(null);
		}

		private void HmacForSqlButton_Click(object sender, RoutedEventArgs e)
		{
			Prepare<HMAC_for_SQL>();
			var result = HMAC_for_SQL.ProcessArguments(null);
		}

		private void TestDomainsButton_Click(object sender, RoutedEventArgs e)
		{
			Prepare<Test_Domains>();
			var result = Test_Domains.ProcessArguments(null);
		}

		private void TestSSLSupportButton_Click(object sender, RoutedEventArgs e)
		{
			Prepare<Test_SSL_Support>();
			var result = Test_SSL_Support.ProcessArguments(null);
		}

		private void TestDomainTlsSupportButton_Click(object sender, RoutedEventArgs e)
		{
			Prepare<Test_Domain_TLS_Support>();
			var result = Test_Domain_TLS_Support.ProcessArguments(new string[] { "/computers=PC15100" });
		}

		private void RsaForSqlButton_Click(object sender, RoutedEventArgs e)
		{
			Prepare<RSA_for_SQL>();
			var result = RSA_for_SQL.ProcessArguments();
		}
	}
}

