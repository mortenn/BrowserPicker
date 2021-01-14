using System;
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
			if(arguments[1] == "/choose")
			{
				TargetURL = arguments[2];
				forceChoice = true;
			}
			else
				TargetURL = arguments.Length > 1 ? arguments[1] : null;
			UnderlyingTargetURL = GetUnderlyingURL(TargetURL);
			AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
			ViewModel = new ViewModel(forceChoice);
			Deactivated += (sender, args) => ViewModel.OnDeactivated();
		}

		private string GetUnderlyingURL(string url)
		{
			if (url.StartsWith("https://staticsint.teams.cdn.office.net/evergreen-assets/safelinks/"))
			{
				var uri = new Uri(url);
				var queryString = uri.Query;
				if (queryString[0] == '?')
				{
					queryString = queryString.Substring(1);
				}
				var queryStringElements = queryString.Split('&');
				foreach (var queryStringElement in queryStringElements)
				{
					var parts = queryStringElement.Split('=');
					if (parts.Length == 2 && parts[0] == "url")
					{
						var underlyingUrl = HttpUtility.UrlDecode(parts[1]);
						return underlyingUrl;
					}
			}
		}
			return url;
		}

		private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
		{
			var e = (Exception) unhandledExceptionEventArgs.ExceptionObject;
			while(e != null)
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
