using System;
using System.Windows;
using System.Windows.Input;

namespace BrowserPicker
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{
		public MainWindow()
		{
			InitializeComponent();
			DataContext = ((App)Application.Current).ViewModel;
		}

		private void MainWindow_OnKeyUp(object sender, KeyEventArgs e)
		{
			try
			{
				if (e.Key == Key.Escape)
					Close();

				var n = 0;
				switch (e.Key)
				{
					case Key.D1: n = 1; break;
					case Key.D2: n = 2; break;
					case Key.D3: n = 3; break;
					case Key.D4: n = 4; break;
					case Key.D5: n = 5; break;
					case Key.D6: n = 6; break;
					case Key.D7: n = 7; break;
					case Key.D8: n = 8; break;
					case Key.D9: n = 9; break;
				}

				if (n > 0 && ViewModel.Choices.Count >= n)
					ViewModel.Choices[n - 1].Select.Execute(null);
			}
			catch
			{
				// ignored
			}
		}

		private ViewModel ViewModel => (ViewModel)DataContext;
	}
}
