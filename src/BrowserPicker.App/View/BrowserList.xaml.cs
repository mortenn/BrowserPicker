using System.Windows;
using System.Windows.Threading;
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

	private void BrowserListScroll_SizeChanged(object? sender, SizeChangedEventArgs e)
	{
		// Update padding after layout so ExtentHeight/ViewportHeight are valid; only reserve space when scrollbar is visible.
		Dispatcher.BeginInvoke(() =>
		{
			if (BrowserListScroll == null) return;
			var pad = BrowserListScroll.ExtentHeight > BrowserListScroll.ViewportHeight && BrowserListScroll.ViewportHeight > 0
				? new Thickness(0, 0, 18, 0) : new Thickness(0);
			if (BrowserListScroll.Padding != pad)
				BrowserListScroll.Padding = pad;
		}, DispatcherPriority.Loaded);
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
}
