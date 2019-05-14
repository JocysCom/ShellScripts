using System;
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
			Properties.Settings.Default.EnvironmentTextBoxText  = EnvironmentTextBox.Text;
			Properties.Settings.Default.Save();
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			SaveSettings();
		}

		private void TransformButton_Click(object sender, RoutedEventArgs e)
		{
			var transforms = XML_Transform.GetTransforms(FolderTextBox.Text);
			XML_Transform.TransformFolder(transforms, EnvironmentTextBox.Text);
			StatusLabel.Content = string.Format("{0:yyyy-MM-dd HH:mm:ss}: Done", DateTime.Now);
		}
	}
}
