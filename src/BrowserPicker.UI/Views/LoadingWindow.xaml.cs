using System.ComponentModel;
using System.Windows;

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
		WindowPlacement.Attach(this);
	}

	private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (e.PreviousSize == e.NewSize)
			return;

		WindowPlacement.Center(this, e.NewSize);
	}
}
