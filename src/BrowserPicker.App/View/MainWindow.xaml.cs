using System.ComponentModel;
using System.Dynamic;
using System.Windows;
using System.Windows.Input;
using JetBrains.Annotations;
using BrowserPicker.ViewModel;
using System.Linq;

namespace BrowserPicker.View;

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

	private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
	{
		ViewModel.AltPressed = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
	}

	private void MainWindow_OnPreviewKeyUp(object sender, KeyEventArgs e)
	{
		try
		{
			ViewModel.AltPressed = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
			if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl && e.Key != Key.LeftShift && e.Key != Key.RightShift)
			{
				var binding =
					(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ? "Ctrl+" : string.Empty)
					+ (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? "Shift+" : string.Empty)
					+ TypeDescriptor.GetConverter(typeof(Key)).ConvertToInvariantString(e.Key);

				var configured = ViewModel.Choices.FirstOrDefault(vm => vm.Model.CustomKeyBind == binding);
				if (configured != null)
				{
					e.Handled = true;
					if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
					{
						configured.SelectPrivacy.Execute(null);
						return;
					}
					configured.Select.Execute(null);
					return;
				}
			}

			if (e.Key == Key.Escape)
			{
				e.Handled = true;
				Close();
				return;
			}

			if (ViewModel.Url.TargetURL == null)
				return;

			int n;
			// ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
			switch (e.Key == Key.System ? e.SystemKey : e.Key)
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
				case Key.C:
					e.Handled = true;
					ViewModel.CopyUrl.Execute(null);
					return;
				default: return;
			}

			var choices = ViewModel.Choices.Where(vm => !vm.Model.Disabled).ToArray();

			if (choices.Length < n)
				return;

			e.Handled = true;
			if (ViewModel.AltPressed)
			{
				choices[n - 1].SelectPrivacy.Execute(null);
			}
			else
			{
				choices[n - 1].Select.Execute(null);
			}
		}
		catch
		{
			// ignored
		}
	}

	private ApplicationViewModel ViewModel => (ApplicationViewModel)DataContext;

	private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (e.PreviousSize == e.NewSize)
			return;

		var w = SystemParameters.PrimaryScreenWidth;
		var h = SystemParameters.PrimaryScreenHeight;

		Left = (w - e.NewSize.Width) / 2;
		Top = (h - e.NewSize.Height) / 2;
	}

	private void DragWindow(object sender, MouseButtonEventArgs args)
	{
		DragMove();
	}
}
