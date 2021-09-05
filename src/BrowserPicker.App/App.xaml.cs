using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BrowserPicker.View;
using BrowserPicker.ViewModel;

namespace BrowserPicker
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App
	{
		private const int LoadingWindowDelayMilliseconds = 300;

		public App()
		{
			// Basic unhandled exception catchment
			AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;

			// Get command line arguments and initialize ViewModel
			var arguments = Environment.GetCommandLineArgs().Skip(1).ToList();
			try
			{
				ViewModel = new ApplicationViewModel(arguments);
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
			CancellationTokenSource cts = null;
			Task<Window> loadingWindow = null;
			try
			{
				// Hook up shutdown on the viewmodel to shut down the application
				ViewModel.OnShutdown += ExitApplication;

				// Catch user switching to another window
				Deactivated += (sender, args) => ViewModel.OnDeactivated();

				// Open in configuration mode if user started BrowserPicker directly
				if (ViewModel.TargetURL == null)
				{
					ShowMainWindow();
					return;
				}

				// Create a CancellationToken that cancels after the lookup timeout
				// to limit the amount of time spent looking up underlying URLs
				cts = new CancellationTokenSource(ViewModel.Configuration.UrlLookupTimeoutMilliseconds);
				try
				{
					// Show LoadingWindow after a small delay
					// Goal is to avoid flicker for fast loading sites but to show progress for sites that take longer
					loadingWindow = ShowLoadingWindow(cts.Token);
					await ViewModel.ScanURLAsync(cts.Token);

					// cancel the token to prevent showing LoadingWindow if it is not needed and has not been shown already
					cts.Cancel();

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
				try { ViewModel.OnShutdown -= ExitApplication; } catch { }
				try { cts?.Cancel(); } catch { }
				try { (await loadingWindow)?.Close(); } catch { }
				try { ViewModel.OnShutdown -= ExitApplication; } catch { }
				ShowExceptionReport(exception);
			}
		}

		/// <summary>
		/// Tells the ViewModel it can initialize and then show the browser list window
		/// </summary>
		private void ShowMainWindow()
		{
			ViewModel.Initialize();
			MainWindow = new MainWindow();
			MainWindow.DataContext = ViewModel;
			MainWindow.Show();
			MainWindow.Focus();
		}

		/// <summary>
		/// Shows the loading message window after a short delay, to let the user know we are in fact working on it
		/// </summary>
		/// <param name="cancellationToken">token that will cancel when the loading is complete or timed out</param>
		/// <returns>The loading message window, so it may be closed.</returns>
		private async Task<Window> ShowLoadingWindow(CancellationToken cancellationToken)
		{
			await Task.Delay(LoadingWindowDelayMilliseconds, cancellationToken);
			var window = new LoadingWindow();
			window.Show();
			return window;
		}

		private void ShowExceptionReport(Exception exception)
		{
			var viewModel = new ExceptionViewModel(exception);
			var window = new ExceptionReport();
			viewModel.OnWindowClosed += (vm, args) => window.Close();
			window.DataContext = viewModel;
			window.Show();
			window.Focus();
			//window.Wait();
		}

		/// <summary>
		/// Bare bones exception handler
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="unhandledExceptionEventArgs"></param>
		private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs unhandledException)
		{
			MessageBox.Show(unhandledException.ExceptionObject.ToString());
		}

		private static void ExitApplication(object sender, EventArgs args)
		{
			Current.Shutdown();
		}

		public ApplicationViewModel ViewModel { get; }
	}
}
