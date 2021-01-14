using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;

namespace BrowserPicker
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App
	{
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
			UnderlyingTargetURL = await GetUnderlyingURLAsync(TargetURL);
			Deactivated += (sender, args) => ViewModel.OnDeactivated();

			ViewModel.Initialize();

			MainWindow = new MainWindow();
			MainWindow.Show();
		}

		private async Task<string> GetUnderlyingURLAsync(string url)
		{
			if (url.StartsWith("https://staticsint.teams.cdn.office.net/evergreen-assets/safelinks/"))
			{
				var uri = new Uri(url);
				var queryString = uri.Query;
				var queryStringValues = HttpUtility.ParseQueryString(queryString);
				var underlyingUrl = queryStringValues["url"];
				if (underlyingUrl != null)
				{
					return underlyingUrl;
				}
			}
			if (url.StartsWith("https://nam06.safelinks.protection.outlook.com/"))
			{
				var clientHandler = new HttpClientHandler
				{
					AllowAutoRedirect = false
				};
				var client = new HttpClient(clientHandler);
				var cts = new CancellationTokenSource();
				cts.CancelAfter(ViewModel.Configuration.UrlLookupTimeoutMilliseconds);
				try
				{
					var response = await client.GetAsync(url, cts.Token);
					var location = response.Headers.Location;
					if (location != null)
					{
						return location.OriginalString;
					}
				}
				catch(TaskCanceledException)
				{
					// TaskCanceledException occurs when the CancellationToken is triggered before the request completes
					// In this case, skip the lookup to avoid poor user experience
					return url;
				}
			}
			return url;
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
