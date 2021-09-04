using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace BrowserPicker.Lib
{
	public class UrlHandler
	{
		[UsedImplicitly]
		// Design time constructor
		public UrlHandler()
		{
			TargetURL = "https://github.com/mortenn/BrowserPicker";
		}

		public UrlHandler(string requestedUrl)
		{
			TargetURL = requestedUrl;
		}

		/// <summary>
		/// Perform some tests on the Target URL to see if it is an URL shortener
		/// </summary>
		public async Task ScanURLAsync(CancellationToken cancellationToken)
		{
			try
			{
				var url = TargetURL;
				var uri = new Uri(url);
				while (true)
				{
					var jump = ResolveJumpPage(uri);
					if (jump != null)
					{
						UnderlyingTargetURL = jump;
						uri = new Uri(jump);
						continue;
					}

					var shortened = await ResolveShortener(uri, cancellationToken);
					if (shortened != null)
					{
						UnderlyingTargetURL = shortened;
						uri = new Uri(shortened);
						continue;
					}
					
					break;
				}
			}
			catch (TaskCanceledException)
			{
				// TaskCanceledException occurs when the CancellationToken is triggered before the request completes
				// In this case, skip the lookup to avoid poor user experience
			}
		}

		private string ResolveJumpPage(Uri uri)
		{
			foreach (var jumpPage in JumpPages)
			{
				if (!uri.Host.EndsWith(jumpPage.url) && !uri.AbsoluteUri.StartsWith(jumpPage.url))
				{
					continue;
				}
				var queryStringValues = HttpUtility.ParseQueryString(uri.Query);
				var underlyingUrl = queryStringValues[jumpPage.parameter];
				if (underlyingUrl != null)
				{
					return underlyingUrl;
				}
			}
			return null;
		}

		private async Task<string> ResolveShortener(Uri uri, CancellationToken cancellationToken)
		{
			if (UrlShorteners.All(s => !uri.Host.EndsWith(s)))
			{
				return null;
			}
			if (client == null)
			{
				client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
			}
			var response = await client.GetAsync(uri, cancellationToken);
			var location = response.Headers.Location;
			if (location != null)
			{
				return location.OriginalString;
			}
			return null;
		}

		public string TargetURL { get; private set; }
		public string UnderlyingTargetURL { get; private set; }

		private HttpClient client = null;

		private static readonly List<string> UrlShorteners = new List<string>
		{
			"safelinks.protection.outlook.com",
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

		private static readonly List<(string url, string parameter)> JumpPages = new List<(string url, string parameter)>()
		{
			("safelinks.protection.outlook.com", "url"),
			("https://staticsint.teams.cdn.office.net/evergreen-assets/safelinks/", "url"),
			("https://l.facebook.com/l.php", "u")
		};
	}
}
