using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using BrowserPicker.View;

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
			var arguments = Environment.GetCommandLineArgs();
			var forceChoice = false;
			if (arguments[1] == "/choose")
			{
				TargetURL = arguments[2];
				forceChoice = true;
			}
			else
				TargetURL = arguments.Length > 1 ? arguments[1] : null;
			AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
			ViewModel = new ViewModel(forceChoice);
		}

		protected override async void OnStartup(StartupEventArgs e)
		{
			UnderlyingTargetURL = TargetURL;

			// Create a CancellationToken that cancels after the lookup timeout
			// to limit the amount of time spent looking up underlying URLs
			var cts = new CancellationTokenSource();
			cts.CancelAfter(ViewModel.Configuration.UrlLookupTimeoutMilliseconds);

			var _ = ShowLoadingWindowAfterDelayAsync(cts.Token); // fire and forget
			await UpdateUnderlyingURLAsync(cts.Token);
			cts.Cancel(); // cancel to avoid accidentally showing LoadingWindow once Task.Delay finishes

			Deactivated += (sender, args) => ViewModel.OnDeactivated();


			ViewModel.Initialize();

			Window oldWindow = MainWindow;
			MainWindow = new MainWindow();
			MainWindow.Show();
			// I tried hiding this before showing the new window but there was a slight gap
			// This way feels like a more immediate switch
			oldWindow?.Hide();
		}

		private async Task ShowLoadingWindowAfterDelayAsync(CancellationToken cancellationToken)
		{
			try
			{
				// Show LoadingWindow after a small delay
				// Goal is to avoid flicker for fast loading sites but to show progress for sites that take longer
				await Task.Delay(LoadingWindowDelayMilliseconds, cancellationToken);
				MainWindow = new LoadingWindow();
				MainWindow.Show();
			}
			catch (TaskCanceledException)
			{
			}
		}

		/// <summary>
		/// Updates
		/// </summary>
		/// <returns></returns>
		private static async Task UpdateUnderlyingURLAsync(CancellationToken cancellationToken)
		{
			while (true)
			{
				string url = UnderlyingTargetURL;
				if (url.StartsWith("https://staticsint.teams.cdn.office.net/evergreen-assets/safelinks/"))
				{
					var uri = new Uri(url);
					var queryString = uri.Query;
					var queryStringValues = HttpUtility.ParseQueryString(queryString);
					var underlyingUrl = queryStringValues["url"];
					if (underlyingUrl != null)
					{
						UnderlyingTargetURL = underlyingUrl;
						continue;
					}
				}
				else if (url.StartsWith("https://l.facebook.com/l.php"))
				{
					var uri = new Uri(url);
					var queryString = uri.Query;
					var queryStringValues = HttpUtility.ParseQueryString(queryString);
					var underlyingUrl = queryStringValues["u"];
					if (underlyingUrl != null)
					{
						UnderlyingTargetURL = underlyingUrl;
						continue;
					}
				}
				else if (url.StartsWith("https://nam06.safelinks.protection.outlook.com/") || url.StartsWith("https://aka.ms/") || url.StartsWith("https://fwd.olsvc.com") || url.StartsWith("https://t.co/"))
				{
					var clientHandler = new HttpClientHandler {AllowAutoRedirect = false};
					var client = new HttpClient(clientHandler);
					try
					{
						var response = await client.GetAsync(url, cancellationToken);
						var location = response.Headers.Location;
						if (location != null)
						{
							UnderlyingTargetURL = location.OriginalString;
							continue;
						}
					}
					catch (TaskCanceledException)
					{
						// TaskCanceledException occurs when the CancellationToken is triggered before the request completes
						// In this case, skip the lookup to avoid poor user experience
					}
				}

				break;
			}
		}

		private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
		{
			var e = (Exception)unhandledExceptionEventArgs.ExceptionObject;
			while (e != null)
			{
				MessageBox.Show(e.Message + e.StackTrace);
				e = e.InnerException;
			}
		}

		public static string TargetURL { get; private set; } = "https://github.com"; // Design time default
		public static string UnderlyingTargetURL { get; private set; } = "https://github.com"; // Design time default

		public ViewModel ViewModel { get; }
	}
}
