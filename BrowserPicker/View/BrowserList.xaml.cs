using System.Windows;
using System.Windows.Controls;

namespace BrowserPicker.View
{
	/// <summary>
	/// Interaction logic for BrowserList.xaml
	/// </summary>
	public partial class BrowserList
	{
		public BrowserList()
		{
			InitializeComponent();
		}

		private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
		}
	}
}
