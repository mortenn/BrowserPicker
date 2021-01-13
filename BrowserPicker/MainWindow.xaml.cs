using System.Windows;
using System.Windows.Input;
using JetBrains.Annotations;

namespace BrowserPicker
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	[UsedImplicitly]
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

				if (App.TargetURL == null)
					return;

				int n;
				// ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
				switch (e.Key)
				{
					case Key.Enter:
					case Key.D1: n = 1; break;
					case Key.D2: n = 2; break;
					case Key.D3: n = 3; break;
					case Key.D4: n = 4; break;
					case Key.D5: n = 5; break;
					case Key.D6: n = 6; break;
					case Key.D7: n = 7; break;
					case Key.D8: n = 8; break;
					case Key.D9: n = 9; break;
					case Key.C: Clipboard.SetText(ViewModel.TargetURL); return;
					default: return;
				}

				if (ViewModel.Choices.Count < n)
					return;

				var privacy = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
				ViewModel.Choices[n - 1].Select.Execute(privacy);
			}
			catch
			{
				// ignored
			}
		}

		private ViewModel ViewModel => (ViewModel)DataContext;

		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if(e.PreviousSize == e.NewSize)
				return;

			var w = SystemParameters.PrimaryScreenWidth;
			var h = SystemParameters.PrimaryScreenHeight;

			Left = (w - e.NewSize.Width) / 2;
			Top = (h - e.NewSize.Height) / 2;
		}
	}
}
