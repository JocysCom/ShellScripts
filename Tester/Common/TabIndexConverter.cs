using System;
using System.Globalization;
using System.Windows.Data;

namespace JocysCom.ClassLibrary.Controls
{
	/// <summary>
	/// Placeholder converter required by Default.xaml TabItem style.
	/// Returns the value unchanged; real logic (if any) belongs to the shared library.
	/// </summary>
	public class TabIndexConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			=> value;

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> value;
	}
}
