using System.Windows;

namespace BrowserPicker.View;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class LoadingWindow
{
	public LoadingWindow()
	{
		InitializeComponent();
		DataContext = ((App)Application.Current).ViewModel;
	}

	private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (e.PreviousSize == e.NewSize)
			return;

		var w = SystemParameters.PrimaryScreenWidth;
		var h = SystemParameters.PrimaryScreenHeight;

		Left = (w - e.NewSize.Width) / 2;
		Top = (h - e.NewSize.Height) / 2;
	}
}
