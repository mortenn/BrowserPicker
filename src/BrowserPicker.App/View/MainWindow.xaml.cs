using System;
using System.ComponentModel;
using System.Dynamic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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
	/// <summary>Ignore the next N SizeChanged events (programmatic or content-driven resize); only save when the user drags to resize.</summary>
	private int _suppressSizeChangeSaveCount;
	private bool _contentRenderedHandled;
	/// <summary>True only after the user started a resize via the grip; ensures we only turn off AutoSizeWindow on actual user resize.</summary>
	private bool _userInitiatedResize;

	public MainWindow()
	{
		InitializeComponent();
		DataContext = ((App)Application.Current).ViewModel;
		if (App.Settings is INotifyPropertyChanged inpc)
			inpc.PropertyChanged += Settings_PropertyChanged;
		if (ViewModel is INotifyPropertyChanged vmInpc)
			vmInpc.PropertyChanged += ViewModel_PropertyChanged;
		// Apply saved size before window is shown so it isn't overridden by initial layout.
		var settings = App.Settings;
		if (!settings.AutoSizeWindow && settings.WindowWidth >= MinWidth && settings.WindowHeight >= MinHeight)
		{
			SizeToContent = SizeToContent.Manual;
			Width = settings.WindowWidth;
			Height = settings.WindowHeight;
		}
	}

	private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(ApplicationViewModel.ConfigurationMode))
			return;
		_suppressSizeChangeSaveCount = 2;
		// Config mode always uses fixed size (never auto); picker mode uses AutoSizeWindow + saved main size.
		if (ViewModel.ConfigurationMode)
		{
			SizeToContent = SizeToContent.Manual;
			var settings = App.Settings;
			var w = settings.ConfigWindowWidth > 0 ? settings.ConfigWindowWidth : 600;
			var h = settings.ConfigWindowHeight > 0 ? settings.ConfigWindowHeight : 450;
			Width = Math.Max(MinWidth, w);
			Height = Math.Max(MinHeight, h);
			UpdateLayout();
		}
		else
			ApplyWindowSizeMode();
	}

	private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(IApplicationSettings.AutoSizeWindow))
			ApplyWindowSizeMode();
	}

	private void MainWindow_Loaded(object sender, RoutedEventArgs e)
	{
		ApplyWindowSizeMode();
	}

	private void MainWindow_ContentRendered(object sender, EventArgs e)
	{
		if (_contentRenderedHandled)
			return;
		_contentRenderedHandled = true;
		// Re-apply saved size so it sticks after first layout; then center. In config mode we use config size (already set when entering config).
		var settings = App.Settings;
		if (!ViewModel.ConfigurationMode && !settings.AutoSizeWindow && settings.WindowWidth > 0 && settings.WindowHeight > 0)
		{
			Width = Math.Max(MinWidth, settings.WindowWidth);
			Height = Math.Max(MinHeight, settings.WindowHeight);
		}
		CenterWindow(new Size(ActualWidth, ActualHeight));
	}

	private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
	{
		var settings = App.Settings;
		if (ViewModel.ConfigurationMode)
		{
			if (ActualWidth > 0 && ActualHeight > 0)
			{
				settings.ConfigWindowWidth = Math.Max(MinWidth, ActualWidth);
				settings.ConfigWindowHeight = Math.Max(MinHeight, ActualHeight);
			}
		}
		else if (!settings.AutoSizeWindow && ActualWidth > 0 && ActualHeight > 0)
		{
			settings.WindowWidth = Math.Max(MinWidth, ActualWidth);
			settings.WindowHeight = Math.Max(MinHeight, ActualHeight);
		}
	}

	private void ApplyWindowSizeMode()
	{
		_suppressSizeChangeSaveCount = 2;
		var settings = App.Settings;
		if (settings.AutoSizeWindow)
		{
			SizeToContent = SizeToContent.WidthAndHeight;
		}
		else
		{
			SizeToContent = SizeToContent.Manual;
			var w = settings.WindowWidth;
			var h = settings.WindowHeight;
			if (w > 0 && h > 0)
			{
				Width = Math.Max(MinWidth, w);
				Height = Math.Max(MinHeight, h);
			}
		}
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

			// When in settings, only handle Escape; let digits/Enter go to focused control (e.g. window size TextBox).
			if (ViewModel.ConfigurationMode)
			{
				if (e.Key == Key.Escape)
				{
					e.Handled = true;
					Close();
				}
				return;
			}

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

		// Ignore size changes caused by ApplyWindowSizeMode or content change (e.g. switching to config); only save on user resize.
		if (_suppressSizeChangeSaveCount > 0)
		{
			_suppressSizeChangeSaveCount--;
			CenterWindow(e.NewSize);
			return;
		}

		// In config mode: persist config size only (never touch AutoSizeWindow). In picker mode: only on user grip resize, turn off auto and save main size.
		if (!_userInitiatedResize || !IsVisible || e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
			return;
		_userInitiatedResize = false;
		var settings = App.Settings;
		if (ViewModel.ConfigurationMode)
		{
			settings.ConfigWindowWidth = Math.Max(MinWidth, e.NewSize.Width);
			settings.ConfigWindowHeight = Math.Max(MinHeight, e.NewSize.Height);
		}
		else
		{
			if (settings.AutoSizeWindow)
				settings.AutoSizeWindow = false;
			settings.WindowWidth = Math.Max(MinWidth, e.NewSize.Width);
			settings.WindowHeight = Math.Max(MinHeight, e.NewSize.Height);
		}
		// Don't re-center on user resize or the window jumps while dragging the grip.
	}

	private void CenterWindow(Size size)
	{
		var w = SystemParameters.PrimaryScreenWidth;
		var h = SystemParameters.PrimaryScreenHeight;
		Left = (w - size.Width) / 2;
		Top = (h - size.Height) / 2;
	}

	private void DragWindow(object sender, MouseButtonEventArgs args)
	{
		if (args.ChangedButton == MouseButton.Left && args.ButtonState == MouseButtonState.Pressed)
			DragMove();
	}

	private void ResizeGrip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		_userInitiatedResize = true;
		// Stop size-to-content so the window doesn't snap back during drag; then start native resize.
		SizeToContent = SizeToContent.Manual;
		var hwnd = new WindowInteropHelper(this).Handle;
		if (hwnd != IntPtr.Zero)
			_ = NativeMethods.SendMessage(hwnd, NativeMethods.WM_SYSCOMMAND, NativeMethods.SC_SIZE_HTBOTTOMRIGHT, IntPtr.Zero);
	}

	private static class NativeMethods
	{
		internal const int WM_SYSCOMMAND = 0x0112;
		// SC_SIZE (0xF000) + hit-test for bottom-right grip; triggers standard OS resize drag.
		internal static readonly IntPtr SC_SIZE_HTBOTTOMRIGHT = (IntPtr)0xF008;

		[DllImport("user32.dll")]
		internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
	}
}
