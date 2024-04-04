using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BrowserPicker.View;
using BrowserPicker.ViewModel;
using BrowserPicker.Windows;

namespace BrowserPicker;

public partial class App
{
	private const int LoadingWindowDelayMilliseconds = 300;

	/// <summary>
	/// This CancellationToken gets cancelled when the application exits
	/// </summary>
	public static CancellationTokenSource ApplicationCancellationToken { get; } = new();

	public static IBrowserPickerConfiguration Settings { get; } = new AppSettings();

	public App()
	{
		BackgroundTasks.Add(Settings);

		// Basic unhandled exception catchment
		AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;

		// Get command line arguments and initialize ViewModel
		var arguments = Environment.GetCommandLineArgs().Skip(1).ToList();
		try
		{
			ViewModel = new ApplicationViewModel(arguments, Settings);
			if (ViewModel.Url != null)
			{
				BackgroundTasks.Add(ViewModel.Url);
			}
		}
		catch (Exception exception)
		{
			ShowExceptionReport(exception);
		}
	}

	protected override async void OnStartup(StartupEventArgs e)
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
			if (string.IsNullOrWhiteSpace(ViewModel.Url?.TargetURL))
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

				// close loading window if it got opened
				(await loadingWindow)?.Close();
			}
			catch (TaskCanceledException)
			{
				// ignored
			}

			// Open up the browser picker window
			ShowMainWindow();
		}
		catch (Exception exception)
		{
			try { if (urlLookup != null) await urlLookup.CancelAsync(); } catch { /* ignored */ }
			try { if (loadingWindow != null) (await loadingWindow)?.Close(); } catch { /* ignored */ }
			try { if (ViewModel != null) ViewModel.OnShutdown -= ExitApplication; } catch { /* ignored */ }
			ShowExceptionReport(exception);
		}
	}

	public static async Task RunLongRunningProcesses()
	{
		try
		{
			var tasks = BackgroundTasks.Select(task => task.Start(ApplicationCancellationToken.Token)).ToArray();
			await Task.WhenAll(tasks);
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

	private static readonly List<ILongRunningProcess> BackgroundTasks = [];
	private static Task? long_running_processes;
}
