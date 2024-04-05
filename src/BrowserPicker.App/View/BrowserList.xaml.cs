using System.Windows;
using BrowserPicker.ViewModel;

namespace BrowserPicker.View;

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

	private void Editor_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
	{
		if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Return)
		{
			if (DataContext is ApplicationViewModel app)
			{
				app.EndEdit.Execute(null);
			}
			e.Handled = true;
			return;
		}
	}
}
