using System.Windows;
using System.Windows.Threading;

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
}
