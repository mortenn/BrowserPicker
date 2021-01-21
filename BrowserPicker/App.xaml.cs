using System;
using System.Collections.Generic;
using System.Linq;
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
			AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
			var arguments = Environment.GetCommandLineArgs().Skip(1).ToList();
			var options = arguments.Where(arg => arg[0] == '/').ToList();
			TargetURL = arguments.Except(options).FirstOrDefault();
			ViewModel = new ViewModel(options.Contains("/choose"));
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
				var url = UnderlyingTargetURL;
				var uri = new Uri(url);
				foreach (var service in JumpPages)
				{
					if (!url.StartsWith(service.Key))
						continue;

					var queryStringValues = HttpUtility.ParseQueryString(uri.Query);
					var underlyingUrl = queryStringValues["url"];
					if (underlyingUrl != null)
						UnderlyingTargetURL = underlyingUrl;
				}
				if(UnderlyingTargetURL != url)
					continue;
				
				if (UrlShorteners.Contains(uri.Host))
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

		private static void CurrentDomainOnUnhandledException(object sender,
			UnhandledExceptionEventArgs unhandledExceptionEventArgs)
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

		private static readonly List<string> UrlShorteners = new List<string>
		{
			"nam06.safelinks.protection.outlook.com",
			"aka.ms",
			"fwd.olsvc.com",
			"t.co",
			"bit.ly",
			"goo.gl",
			"tinyurl.com",
			"ow.ly",
			"is.gd",
			"buff.ly",
			"adf.ly",
			"bit.do",
			"mcaf.ee",
			"su.pr",
			"go.microsoft.com"
		};

		private static readonly Dictionary<string, string> JumpPages = new Dictionary<string, string>
		{
			{"https://staticsint.teams.cdn.office.net/evergreen-assets/safelinks/", "url"},
			{"https://l.facebook.com/l.php", "u"}
		};
	}
}
