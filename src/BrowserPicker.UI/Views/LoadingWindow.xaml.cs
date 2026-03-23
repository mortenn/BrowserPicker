using System.Windows;
using System.ComponentModel;

namespace BrowserPicker.UI.Views;

/// <summary>
/// Interaction logic for LoadingWindow.xaml
/// </summary>
public partial class LoadingWindow
{
	public LoadingWindow()
	{
		InitializeComponent();
		if (DesignerProperties.GetIsInDesignMode(this) || Application.Current is not App { ViewModel: not null })
			return;
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
