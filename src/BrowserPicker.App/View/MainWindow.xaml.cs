using System;
using System.ComponentModel;
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
	/// <summary>Ignore the next N SizeChanged events (programmatic or content-driven resize); only save when the user drags to resize.</summary>
	private int suppress_size_change_save_count;
	private bool content_rendered_handled;
	/// <summary>True only after the user started a resize via the grip; ensures we only turn off AutoSizeWindow on actual user resize.</summary>
	private bool user_initiated_resize;
	/// <summary>True while the user is dragging the resize grip; we update Width/Height from mouse position.</summary>
	private bool resizing_via_grip;

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
		if (settings.AutoSizeWindow || !(settings.WindowWidth >= MinWidth) || !(settings.WindowHeight >= MinHeight))
		{
			return;
		}

		SizeToContent = SizeToContent.Manual;
		Width = settings.WindowWidth;
		Height = settings.WindowHeight;
	}

	private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(ApplicationViewModel.ConfigurationMode))
			return;
		suppress_size_change_save_count = 2;
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
		{
			// Persist config size when leaving config so next time we enter we restore it.
			if (ActualWidth > 0 && ActualHeight > 0)
			{
				var settings = App.Settings;
				settings.ConfigWindowWidth = Math.Max(MinWidth, ActualWidth);
				settings.ConfigWindowHeight = Math.Max(MinHeight, ActualHeight);
			}
			ApplyWindowSizeMode();
		}
	}

	private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(IApplicationSettings.AutoSizeWindow) && !ViewModel.ConfigurationMode)
			ApplyWindowSizeMode();
	}

	private void MainWindow_Loaded(object sender, RoutedEventArgs e)
	{
		if (!ViewModel.ConfigurationMode)
			ApplyWindowSizeMode();
	}

	private void MainWindow_ContentRendered(object sender, EventArgs e)
	{
		if (content_rendered_handled)
			return;
		content_rendered_handled = true;
		// Re-apply saved size so it sticks after first layout; then center. In config mode we use config size (already set when entering config).
		var settings = App.Settings;
		if (!ViewModel.ConfigurationMode && settings is { AutoSizeWindow: false, WindowWidth: > 0, WindowHeight: > 0 })
		{
			Width = Math.Max(MinWidth, settings.WindowWidth);
			Height = Math.Max(MinHeight, settings.WindowHeight);
		}
		CenterWindow(new Size(ActualWidth, ActualHeight));
	}

	private void MainWindow_Closing(object? sender, CancelEventArgs e)
	{
		var settings = App.Settings;
		if (ViewModel.ConfigurationMode)
		{
			if (!(ActualWidth > 0) || !(ActualHeight > 0))
			{
				return;
			}

			settings.ConfigWindowWidth = Math.Max(MinWidth, ActualWidth);
			settings.ConfigWindowHeight = Math.Max(MinHeight, ActualHeight);
			return;
		}
		if (!settings.AutoSizeWindow && ActualWidth > 0 && ActualHeight > 0)
		{
			settings.WindowWidth = Math.Max(MinWidth, ActualWidth);
			settings.WindowHeight = Math.Max(MinHeight, ActualHeight);
		}
	}

	private void ApplyWindowSizeMode()
	{
		if (ViewModel.ConfigurationMode)
			return;
		suppress_size_change_save_count = 2;
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
		if (suppress_size_change_save_count > 0)
		{
			suppress_size_change_save_count--;
			CenterWindow(e.NewSize);
			return;
		}

		if (!IsVisible || e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
			return;
		var settings = App.Settings;
		if (ViewModel.ConfigurationMode)
		{
			// In config mode: persist config size on any resize (grip or window border).
			settings.ConfigWindowWidth = Math.Max(MinWidth, e.NewSize.Width);
			settings.ConfigWindowHeight = Math.Max(MinHeight, e.NewSize.Height);
			return;
		}
		// In picker mode: only save main size when user resized via the grip (don't turn off auto on layout/startup).
		if (!user_initiated_resize)
			return;
		user_initiated_resize = false;
		if (settings.AutoSizeWindow)
			settings.AutoSizeWindow = false;
		settings.WindowWidth = Math.Max(MinWidth, e.NewSize.Width);
		settings.WindowHeight = Math.Max(MinHeight, e.NewSize.Height);
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
		if (args is { ChangedButton: MouseButton.Left, ButtonState: MouseButtonState.Pressed })
			DragMove();
	}

	private void ResizeGrip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
		user_initiated_resize = true;
		SizeToContent = SizeToContent.Manual;
		resizing_via_grip = true;
		Mouse.Capture(this, CaptureMode.SubTree);
		MouseMove += Window_ResizeGripMouseMove;
		MouseLeftButtonUp += Window_ResizeGripMouseUp;
		LostMouseCapture += Window_ResizeGripLostCapture;
	}

	private void Window_ResizeGripMouseMove(object sender, MouseEventArgs e)
	{
		if (!resizing_via_grip)
			return;
		var pt = PointToScreen(Mouse.GetPosition(this));
		Width = Math.Max(MinWidth, pt.X - Left);
		Height = Math.Max(MinHeight, pt.Y - Top);
	}

	private void Window_ResizeGripMouseUp(object sender, MouseButtonEventArgs e)
	{
		EndResizeGripDrag();
	}

	private void Window_ResizeGripLostCapture(object sender, MouseEventArgs e)
	{
		EndResizeGripDrag();
	}

	private void EndResizeGripDrag()
	{
		if (!resizing_via_grip)
			return;
		resizing_via_grip = false;
		MouseMove -= Window_ResizeGripMouseMove;
		MouseLeftButtonUp -= Window_ResizeGripMouseUp;
		LostMouseCapture -= Window_ResizeGripLostCapture;
		ReleaseMouseCapture();
	}
}
