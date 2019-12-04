using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace JocysCom.Shell.Scripts.Tester
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

		private void IsPortOpenButton_Click(object sender, RoutedEventArgs e)
		{
			Prepare<IsPortOpen>();
			var result = IsPortOpen.ProcessArguments(new string[] { "/TaskFile=Google.xml" });
		}

		async private void TestAsyncButton_Click(object sender, RoutedEventArgs e)
		{
			// Create a TaskScheduler that wraps the SynchronizationContext returned from
			// System.Threading.SynchronizationContext.Current
			// This is an object that handles the low-level work of queuing tasks onto main User Interface(GUI) thread.
			var mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
			TestAsyncTextBox.Text = string.Format("{0:mm:ss.fff}", DateTime.Now);
			// Execute the task and then continue to execute lines on the same thread which started the task.
			var result = await TestTaskAsync(1).ConfigureAwait(true);
			TestAsyncTextBox.Text += ", " + result;
			// Execute the task and continue execute lines on the thread which was used to do the task.
			result = await TestTaskAsync(2).ConfigureAwait(false);
			try
			{
				// This line will crash because task thread cannot access TextBox, because it belongs to the main GUI thread.
				TestAsyncTextBox.Text += ", " + result;
			}
			catch (Exception ex)
			{
				//
				await Task.Factory.StartNew(() =>
					{
						TestAsyncErrorTextBox.Text = ex.Message;
						TestAsyncErrorTextBox.Text += "\r\n, " + result;
					},
					System.Threading.CancellationToken.None,
					TaskCreationOptions.DenyChildAttach, mainTaskScheduler
				);
			}
		}

		async Task<string> TestTaskAsync(int value)
		{
			return await Task.Run(() =>
			{
				return TestTask(value);
			}).ConfigureAwait(true);
		}

		string TestTask(int value)
		{
			// Create 5 second delay;
			var i = 0;
			var sw = new Stopwatch();
			sw.Start();
			while (sw.ElapsedMilliseconds < 3000)
				i++;
			return string.Format("{0}: {1}", value, i);
		}

		private void TestSyncButton_Click(object sender, RoutedEventArgs e)
		{
			var mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
			var result = TestTask(1);
			TestAsyncTextBox.Text = result;
		}

		private void TestSyncFoldersButton_Click(object sender, RoutedEventArgs e)
		{
			Prepare<Sync_Folders>();
			var result = Sync_Folders.ProcessArguments(new string[] { "/source=.\\Source", "/target=.\\Target", "/save_dates" });
		}
	}
}

