using System.Windows;
using System.Windows.Threading;

namespace BrowserPicker.UI.Views;

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
		Dispatcher.BeginInvoke(() =>
		{
			if (BrowserListScroll == null) return;
			var pad = BrowserListScroll.ExtentHeight > BrowserListScroll.ViewportHeight && BrowserListScroll.ViewportHeight > 0
				? new Thickness(0, 0, 18, 0) : new Thickness(0);
			if (BrowserListScroll.Padding != pad)
				BrowserListScroll.Padding = pad;
		}, DispatcherPriority.Loaded);
	}
}
