using System.Windows;
using System.Windows.Input;

namespace BrowserPicker
{
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

		private ViewModel ViewModel => (ViewModel)DataContext;

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
}
