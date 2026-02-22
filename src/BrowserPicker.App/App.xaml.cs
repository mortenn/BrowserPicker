using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using BrowserPicker.View;
using BrowserPicker.ViewModel;
using Microsoft.Win32;

namespace BrowserPicker;

public partial class App
{
	private const int LoadingWindowDelayMilliseconds = 300;

	/// <summary>
	/// This CancellationToken gets cancelled when the application exits
	/// </summary>
	private static CancellationTokenSource ApplicationCancellationToken { get; } = new();

	public static IBrowserPickerConfiguration Settings { get; set; } = null!;

	/// <summary>Content area brushes updated when ThemeMode changes so inherited text/background are readable (avoids white-on-white).</summary>
	public static readonly string ContentBackgroundBrushKey = "ContentBackgroundBrush";
	/// <summary>Semi-transparent content background used when transparency is enabled (DisableTransparency = false).</summary>
	public static readonly string ContentBackgroundSemiTransparentBrushKey = "ContentBackgroundSemiTransparentBrush";
	public static readonly string ContentForegroundBrushKey = "ContentForegroundBrush";

	private class InvalidUTF8Patch : EncodingProvider
	{
		public override Encoding? GetEncoding(int codepage)
		{
			return null;
		}

		public override Encoding? GetEncoding(string name)
		{
			return name.ToLowerInvariant().Replace("-", "") switch
			{
				"utf8" => Encoding.UTF8,
				_ => null
			};
		}
	}

	public App()
	{
		Encoding.RegisterProvider(new InvalidUTF8Patch());
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		BackgroundTasks.Add(Settings);

		// Basic unhandled exception catchment
		AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
		DispatcherUnhandledException += OnDispatcherUnhandledException;

		// Get command line arguments and initialize ViewModel
		var arguments = Environment.GetCommandLineArgs().Skip(1).ToList();
		try
		{
			ViewModel = new ApplicationViewModel(arguments, Settings);
			if (ViewModel.Url.TargetURL != null)
			{
				BackgroundTasks.Add(ViewModel.Url);
			}
		}
		catch (Exception exception)
		{
			ShowExceptionReport(exception);
		}
	}

	/// <summary>Add content theme brushes to app resources. Call from Program before Run() so they exist when windows load.</summary>
	internal void AddContentThemeDictionary(ThemeMode mode)
	{
		var useLight = mode switch
		{
			BrowserPicker.ThemeMode.Light => true,
			BrowserPicker.ThemeMode.Dark => false,
			_ => IsSystemUsingLightTheme()
		};
		Resources.MergedDictionaries.Add(CreateContentThemeDictionary(useLight));
	}

	protected override void OnStartup(StartupEventArgs e)
	{
		ApplyThemeMode(Settings.ThemeMode);
		Settings.PropertyChanged += Settings_PropertyChanged;
		var worker = StartupBackgroundTasks();
		worker.ContinueWith(CheckBackgroundTasks);
	}

	private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(IApplicationSettings.ThemeMode) || sender is not IApplicationSettings s)
			return;
		var mode = s.ThemeMode;
		// Apply on UI thread so theme and DynamicResource updates take effect immediately.
		Dispatcher.BeginInvoke(() => ApplyThemeMode(mode), DispatcherPriority.Loaded);
	}

	/// <summary>Apply user's theme choice. Fluent theme is designed to follow ThemeMode — no custom brush overrides.</summary>
	private static void ApplyThemeMode(ThemeMode mode)
	{
		var wpfMode = mode switch
		{
			BrowserPicker.ThemeMode.Light => System.Windows.ThemeMode.Light,
			BrowserPicker.ThemeMode.Dark => System.Windows.ThemeMode.Dark,
			_ => System.Windows.ThemeMode.System
		};
#pragma warning disable WPF0001
		var app = Current;
		app.ThemeMode = wpfMode;
		if (app.MainWindow != null)
			app.MainWindow.ThemeMode = wpfMode;
#pragma warning restore WPF0001
		// Content brushes: swap theme dictionary so DynamicResource re-resolves (in-place replace doesn't refresh UI).
		var useLight = mode switch
		{
			BrowserPicker.ThemeMode.Light => true,
			BrowserPicker.ThemeMode.Dark => false,
			_ => IsSystemUsingLightTheme()
		};
		SwapContentThemeDictionary(Current.Resources, useLight);
		// Force visual refresh so updated theme brushes are applied.
		InvalidateContentTheme();
	}

	private static bool IsSystemUsingLightTheme()
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
			var value = key?.GetValue("AppsUseLightTheme");
			return value is int i && i != 0;
		}
		catch
		{
			return true;
		}
	}

	private static void SwapContentThemeDictionary(ResourceDictionary appResources, bool light)
	{
		// Content brushes live only in our theme dict (not in ResourceDictionary.xaml, which keeps only images).
		// Remove previous theme dict if present (last merged), then add current theme.
		var merged = appResources.MergedDictionaries;
		if (merged.Count > 0 && merged[^1].Contains(ContentBackgroundSemiTransparentBrushKey))
			merged.RemoveAt(merged.Count - 1);
		merged.Add(CreateContentThemeDictionary(light));
	}

	private static ResourceDictionary CreateContentThemeDictionary(bool light)
	{
		var d = new ResourceDictionary();
		const byte semiTransparentAlpha = 0xE6; // ~90% opaque for slight see-through
		if (light)
		{
			// Slightly off-white so it's not stark; still clearly light.
			d[ContentBackgroundBrushKey] = new SolidColorBrush(Color.FromRgb(0xEB, 0xEB, 0xEB));
			d[ContentBackgroundSemiTransparentBrushKey] = new SolidColorBrush(Color.FromArgb(semiTransparentAlpha, 0xEB, 0xEB, 0xEB));
			d[ContentForegroundBrushKey] = new SolidColorBrush(Colors.Black);
		}
		else
		{
			d[ContentBackgroundBrushKey] = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));
			d[ContentBackgroundSemiTransparentBrushKey] = new SolidColorBrush(Color.FromArgb(semiTransparentAlpha, 0x2D, 0x2D, 0x2D));
			d[ContentForegroundBrushKey] = new SolidColorBrush(Colors.White);
		}
		return d;
	}

	/// <summary>Returns current content theme brushes (for code-behind so Configuration always gets correct colors at runtime).</summary>
	internal static void GetContentThemeBrushes(out SolidColorBrush background, out SolidColorBrush foreground)
	{
		var useLight = Settings.ThemeMode switch
		{
			BrowserPicker.ThemeMode.Light => true,
			BrowserPicker.ThemeMode.Dark => false,
			_ => IsSystemUsingLightTheme()
		};
		if (useLight)
		{
			background = new SolidColorBrush(Color.FromRgb(0xEB, 0xEB, 0xEB));
			foreground = new SolidColorBrush(Colors.Black);
		}
		else
		{
			background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));
			foreground = new SolidColorBrush(Colors.White);
		}
	}

	/// <summary>Invalidate visual tree so DynamicResource re-resolves for content theme brushes.</summary>
	private static void InvalidateContentTheme()
	{
		var main = Current.MainWindow;
		if (main == null) return;
		main.InvalidateVisual();
		main.UpdateLayout();
		foreach (Window w in Current.Windows)
		{
			if (w == main)
			{
				continue;
			}

			w.InvalidateVisual();
			w.UpdateLayout();
		}
	}

	/// <summary>
	/// This method should never be called, as the StartupBackgroundTasks has robust exception handling
	/// </summary>
	private static void CheckBackgroundTasks(Task task)
	{
		if (task.IsFaulted)
		{
			MessageBox.Show(
				task.Exception?.ToString() ?? string.Empty,
				"Error",
				MessageBoxButton.OK,
				MessageBoxImage.Error
			);
		}
	}
	
	private async Task StartupBackgroundTasks()
	{
		// Something failed during startup, abort.
		if (ViewModel == null)
		{
			return;
		}
		CancellationTokenSource? urlLookup = null;
		Task<Window?>? loadingWindow = null;
		try
		{
			// Hook up shutdown on the viewmodel to shut down the application
			ViewModel.OnShutdown += ExitApplication;

			// Catch user switching to another window
			Deactivated += (_, _) => ViewModel.OnDeactivated();

			long_running_processes = RunLongRunningProcesses();

			// Open in configuration mode if user started BrowserPicker directly
			if (string.IsNullOrWhiteSpace(ViewModel.Url.TargetURL))
			{
				ShowMainWindow();
				return;
			}

			// Create a CancellationToken that cancels after the lookup timeout
			// to limit the amount of time spent looking up underlying URLs
			urlLookup = ViewModel.Configuration.GetUrlLookupTimeout();
			try
			{
				// Show LoadingWindow after a small delay
				// Goal is to avoid flicker for fast loading sites but to show progress for sites that take longer
				loadingWindow = ShowLoadingWindow(urlLookup.Token);

				// Wait for long-running processes in case they finish quickly
				await Task.Run(() => long_running_processes.Wait(ApplicationCancellationToken.Token), urlLookup.Token);

				// cancel the token to prevent showing LoadingWindow if it is not needed and has not been shown already
				await urlLookup.CancelAsync();

				ShowMainWindow();

				// close loading window if it got opened
				var waited = await loadingWindow;
				waited?.Close();
			}
			catch (TaskCanceledException)
			{
				// Open up the browser picker window
				ShowMainWindow();
			}
		}
		catch (Exception exception)
		{
			try { if (urlLookup != null) await urlLookup.CancelAsync(); } catch { /* ignored */ }
			try { if (loadingWindow != null) (await loadingWindow)?.Close(); } catch { /* ignored */ }
			try { if (ViewModel != null) ViewModel.OnShutdown -= ExitApplication; } catch { /* ignored */ }
			ShowExceptionReport(exception);
		}
	}

	private static async Task RunLongRunningProcesses()
	{
		try
		{
			var tasks = BackgroundTasks.Select(task => task.Start(ApplicationCancellationToken.Token)).ToArray();
			await Task.WhenAll(tasks);
			foreach (var task in tasks)
			{
				await task;
			}
		}
		catch (TaskCanceledException)
		{
			// ignored
		}
	}

	/// <summary>
	/// Tells the ViewModel it can initialize and then show the browser list window
	/// </summary>
	private void ShowMainWindow()
	{
		ViewModel?.Initialize();
		MainWindow = new MainWindow
		{
			DataContext = ViewModel
		};
		MainWindow.Show();
		MainWindow.Focus();
	}

	/// <summary>
	/// Shows the loading message window after a short delay, to let the user know we are in fact working on it
	/// </summary>
	/// <param name="cancellationToken">token that will cancel when the loading is complete or timed out</param>
	/// <returns>The loading message window, so it may be closed.</returns>
	private static async Task<Window?> ShowLoadingWindow(CancellationToken cancellationToken)
	{
		try
		{
			await Task.Delay(LoadingWindowDelayMilliseconds, cancellationToken);
		}
		catch (TaskCanceledException)
		{
			return null;
		}
		var window = new LoadingWindow();
		window.Show();
		return window;
	}

	private static void ShowExceptionReport(Exception exception)
	{
		var viewModel = new ExceptionViewModel(exception);
		var window = new ExceptionReport();
		viewModel.OnWindowClosed += (_, _) => window.Close();
		window.DataContext = viewModel;
		window.Show();
		window.Focus();
	}

	/// <summary>
	/// Bare-bones exception handler
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="unhandledException"></param>
	private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs unhandledException)
	{
		ApplicationCancellationToken.Cancel();
		_ = MessageBox.Show(unhandledException.ExceptionObject.ToString());
	}

	private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		ApplicationCancellationToken.Cancel();
		_ = MessageBox.Show(e.Exception.ToString());
	}

	private static void ExitApplication(object? sender, EventArgs args)
	{
		ApplicationCancellationToken.Cancel();
		try
		{
			long_running_processes?.Wait();
		}
		catch (TaskCanceledException)
		{
			// ignore;
		}
		Current.Shutdown();
	}

	public ApplicationViewModel? ViewModel { get; }
	public static IServiceProvider Services { get; set; } = null!;

	private static readonly List<ILongRunningProcess> BackgroundTasks = [];
	private static Task? long_running_processes;
}
