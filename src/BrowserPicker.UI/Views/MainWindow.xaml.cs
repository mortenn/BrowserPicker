using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BrowserPicker.Common;
using BrowserPicker.UI.ViewModels;
using JetBrains.Annotations;

namespace BrowserPicker.UI.Views;

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
	private Point resize_start_screen;
	private Size resize_start_size;
	private DispatcherTimer? resize_mouse_button_watchdog;

	public MainWindow()
	{
		InitializeComponent();
		WindowPlacement.Attach(this);
		if (DesignerProperties.GetIsInDesignMode(this) || Application.Current is not App { ViewModel: not null })
			return;
		DataContext = ((App)Application.Current).ViewModel;
		if (App.Settings is INotifyPropertyChanged monitored)
		{
			// The window closes when the user switches to the configuration window, so stop listening then.
			monitored.PropertyChanged += Settings_PropertyChanged;
			Closed += (_, _) => monitored.PropertyChanged -= Settings_PropertyChanged;
		}
		if (App.Settings is not { } settings)
			return;

		// Apply saved picker size before window is shown so it isn't overridden by initial layout.
		if (settings.AutoSizeWindow || !(settings.WindowWidth >= MinWidth) || !(settings.WindowHeight >= MinHeight))
		{
			return;
		}

		SizeToContent = SizeToContent.Manual;
		Width = settings.WindowWidth;
		Height = settings.WindowHeight;
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
		if (content_rendered_handled)
			return;
		content_rendered_handled = true;
		// Re-apply saved size so it sticks after first layout; then center.
		if (App.Settings is { AutoSizeWindow: false, WindowWidth: > 0, WindowHeight: > 0 } settings)
		{
			Width = Math.Max(MinWidth, settings.WindowWidth);
			Height = Math.Max(MinHeight, settings.WindowHeight);
		}
		CenterWindow(new Size(ActualWidth, ActualHeight));
	}

	private void MainWindow_Closing(object? sender, CancelEventArgs e)
	{
		if (App.Settings is not { } settings)
			return;
		if (settings.AutoSizeWindow || ActualWidth <= 0 || ActualHeight <= 0)
		{
			return;
		}

		settings.WindowWidth = Math.Max(MinWidth, ActualWidth);
		settings.WindowHeight = Math.Max(MinHeight, ActualHeight);
	}

	private void ApplyWindowSizeMode()
	{
		if (App.Settings is not { } settings)
			return;
		suppress_size_change_save_count = 2;
		if (settings.AutoSizeWindow)
		{
			SizeToContent = SizeToContent.WidthAndHeight;
			return;
		}

		SizeToContent = SizeToContent.Manual;
		var w = settings.WindowWidth;
		var h = settings.WindowHeight;
		if (w <= 0 || h <= 0)
		{
			return;
		}

		Width = Math.Max(MinWidth, w);
		Height = Math.Max(MinHeight, h);
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
					+ (
						Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)
							? "Shift+"
							: string.Empty
					)
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
				case Key.D1:
					n = 1;
					break;
				case Key.D2:
					n = 2;
					break;
				case Key.D3:
					n = 3;
					break;
				case Key.D4:
					n = 4;
					break;
				case Key.D5:
					n = 5;
					break;
				case Key.D6:
					n = 6;
					break;
				case Key.D7:
					n = 7;
					break;
				case Key.D8:
					n = 8;
					break;
				case Key.D9:
					n = 9;
					break;
				case Key.C:
					e.Handled = true;
					ViewModel.CopyUrl.Execute(null);
					return;
				default:
					return;
			}

			var digitShortcutChoices = ViewModel
				.PickerChoices.Where(o =>
					o switch
					{
						BrowserViewModel { Model: { Disabled: false, Removed: false } } => true,
						BrowserProfileViewModel { ParentBrowser.Model: { Disabled: false, Removed: false } } => true,
						_ => false,
					}
				)
				.ToArray();

			if (digitShortcutChoices.Length < n)
				return;

			e.Handled = true;
			if (ViewModel.AltPressed)
			{
				switch (digitShortcutChoices[n - 1])
				{
					case BrowserViewModel vm:
						vm.SelectPrivacy.Execute(null);
						break;
					case BrowserProfileViewModel pm:
						pm.SelectPrivacy.Execute(null);
						break;
				}
			}
			else
			{
				switch (digitShortcutChoices[n - 1])
				{
					case BrowserViewModel vm:
						vm.Select.Execute(null);
						break;
					case BrowserProfileViewModel pm:
						pm.Select.Execute(null);
						break;
				}
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

		// Ignore size changes caused by ApplyWindowSizeMode or content change; only save on user resize.
		if (suppress_size_change_save_count > 0)
		{
			suppress_size_change_save_count--;
			if (!resizing_via_grip)
			{
				CenterWindow(e.NewSize);
			}
			return;
		}

		if (!IsVisible || e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
			return;
		if (App.Settings is not { } settings)
			return;
		// Only save main size when user resized via the grip (don't turn off auto on layout/startup).
		if (!user_initiated_resize)
			return;
		user_initiated_resize = false;
		if (settings.AutoSizeWindow)
			settings.AutoSizeWindow = false;
		settings.WindowWidth = Math.Max(MinWidth, e.NewSize.Width);
		settings.WindowHeight = Math.Max(MinHeight, e.NewSize.Height);
		// Don't re-center on user resize or the window jumps while dragging the grip.
	}

	private void CenterWindow(Size size) => WindowPlacement.Center(this, size);

	private void DragWindow(object sender, MouseButtonEventArgs args)
	{
		if (args is { ChangedButton: MouseButton.Left, ButtonState: MouseButtonState.Pressed })
			DragMove();
	}

	private void ResizeGrip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
		user_initiated_resize = true;
		suppress_size_change_save_count = 0;
		resizing_via_grip = true;
		resize_start_screen = PointToScreen(e.GetPosition(this));
		resize_start_size = new Size(ActualWidth, ActualHeight);
		var left = Left;
		var top = Top;
		SizeToContent = SizeToContent.Manual;
		Width = Math.Max(MinWidth, resize_start_size.Width);
		Height = Math.Max(MinHeight, resize_start_size.Height);
		Left = left;
		Top = top;
		Mouse.Capture(this, CaptureMode.SubTree);
		MouseMove += Window_ResizeGripMouseMove;
		MouseLeftButtonUp += Window_ResizeGripMouseUp;
		LostMouseCapture += Window_ResizeGripLostCapture;
		StartResizeMouseButtonWatchdog();
	}

	private void Window_ResizeGripMouseMove(object sender, MouseEventArgs e)
	{
		if (!resizing_via_grip)
			return;
		if (e.LeftButton != MouseButtonState.Pressed)
		{
			EndResizeGripDrag();
			return;
		}
		var pt = PointToScreen(e.GetPosition(this));
		Width = Math.Max(GetResizeMinWidth(), resize_start_size.Width + pt.X - resize_start_screen.X);
		Height = Math.Max(MinHeight, resize_start_size.Height + pt.Y - resize_start_screen.Y);
	}

	private double GetResizeMinWidth()
	{
		if (PickerHost is not { IsVisible: true })
		{
			return MinWidth;
		}

		PickerHost.Measure(
			new Size(double.PositiveInfinity, ActualHeight > 0 ? ActualHeight : double.PositiveInfinity)
		);
		return Math.Max(MinWidth, PickerHost.DesiredSize.Width + 20);
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
		resize_mouse_button_watchdog?.Stop();
		ReleaseMouseCapture();
	}

	private void StartResizeMouseButtonWatchdog()
	{
		resize_mouse_button_watchdog ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
		resize_mouse_button_watchdog.Tick -= ResizeMouseButtonWatchdog_Tick;
		resize_mouse_button_watchdog.Tick += ResizeMouseButtonWatchdog_Tick;
		resize_mouse_button_watchdog.Start();
	}

	private void ResizeMouseButtonWatchdog_Tick(object? sender, EventArgs e)
	{
		if (resizing_via_grip && Mouse.LeftButton != MouseButtonState.Pressed)
		{
			EndResizeGripDrag();
		}
	}
}
