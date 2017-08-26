using System.Windows;
using System.Windows.Controls;

namespace BrowserPicker.View
{
	/// <summary>
	/// Interaction logic for Configuration.xaml
	/// </summary>
	public partial class Configuration
	{
		public Configuration()
		{
			InitializeComponent();
		}

		private void AddDefault(object sender, RoutedEventArgs e)
		{
			var fragment = NewFragment.Text;
			var browser = (string)NewDefault.SelectedValue;
			Config.SetDefault(fragment, browser);
			DefaultsList.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.UpdateTarget();
		}
	}
}
