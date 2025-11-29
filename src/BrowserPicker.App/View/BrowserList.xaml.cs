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
		if (e.Key is not (System.Windows.Input.Key.Enter))
		{
			return;
		}

		if (DataContext is ApplicationViewModel app)
		{
			app.EndEdit.Execute(null);
		}
		e.Handled = true;
	}

	private void CheckBox_Checked(object sender, RoutedEventArgs e)
	{

	}
}
