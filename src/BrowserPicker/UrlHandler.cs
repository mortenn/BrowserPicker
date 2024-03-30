using BrowserPicker.Framework;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace BrowserPicker
{
	public class UrlHandler : ModelBase, ILongRunningProcess
	{
		[UsedImplicitly]
		// Design time constructor
		public UrlHandler()
		{
			TargetURL = "https://github.com/mortenn/BrowserPicker";
		}

		public UrlHandler(IBrowserPickerConfiguration configuration, string requestedUrl)
		{

			TargetURL = requestedUrl;
			this.configuration = configuration;
		}

		/// <summary>
		/// Perform some tests on the Target URL to see if it is an URL shortener
		/// </summary>
		public async Task Start(CancellationToken cancellationToken)
		{
			try
			{
				var uri = new Uri(TargetURL);
				while (true)
				{
					var jump = ResolveJumpPage(uri);
					if (jump != null)
					{
						UnderlyingTargetURL = jump;
						uri = new Uri(jump);
						continue;
					}

					if (configuration.DisableNetworkAccess)
						break;

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
				// In this case, end the lookup to avoid poor user experience
			}
		}

		private static string ResolveJumpPage(Uri uri)
		{
			return (
				from jumpPage in JumpPages
				where uri.Host.EndsWith(jumpPage.url) || uri.AbsoluteUri.StartsWith(jumpPage.url)
				let queryStringValues = HttpUtility.ParseQueryString(uri.Query)
				select queryStringValues[jumpPage.parameter]
			).FirstOrDefault(underlyingUrl => underlyingUrl != null);
		}

		private static async Task<string> ResolveShortener(Uri uri, CancellationToken cancellationToken)
		{
			if (UrlShorteners.All(s => !uri.Host.EndsWith(s)))
			{
				return null;
			}
			var response = await client.GetAsync(uri, cancellationToken);
			var location = response.Headers.Location;
			return location?.OriginalString;
		}

		public string TargetURL { get; }

		public string UnderlyingTargetURL
		{
			get => underlying_target_url;
			set
			{
				underlying_target_url = value;
				OnPropertyChanged();
			}
		}

		private static readonly List<string> UrlShorteners =
		[
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
		];

		private static readonly List<(string url, string parameter)> JumpPages =
		[
			("safelinks.protection.outlook.com", "url"),
			("https://staticsint.teams.cdn.office.net/evergreen-assets/safelinks/", "url"),
			("https://l.facebook.com/l.php", "u")
		];
		private readonly IBrowserPickerConfiguration configuration;
		private string underlying_target_url;
		private static readonly HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false });
	}
}
