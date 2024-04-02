using BrowserPicker.Framework;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace BrowserPicker;

public sealed class UrlHandler(IBrowserPickerConfiguration configuration, string requestedUrl)
	: ModelBase, ILongRunningProcess
{
#if DEBUG
	[UsedImplicitly]
	// Design time constructor
	public UrlHandler() : this(null, "https://github.com/mortenn/BrowserPicker")
	{
	}
#endif

	/// <summary>
	/// Perform some tests on the Target URL to see if it is a URL shortener
	/// </summary>
	public async Task Start(CancellationToken cancellationToken)
	{
		try
		{
			var uri = new Uri(TargetURL);
			HostName = uri.Host;
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
					IsShortenedURL = true;
					UnderlyingTargetURL = shortened;
					uri = new Uri(shortened);
					HostName = uri.Host;
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
		var response = await Client.GetAsync(uri, cancellationToken);
		var location = response.Headers.Location;
		return location?.OriginalString;
	}

	public string TargetURL { get; } = requestedUrl;

	public string UnderlyingTargetURL
	{
		get => underlying_target_url;
		set => SetProperty(ref underlying_target_url, value);
	}

	public bool IsShortenedURL
	{
		get => is_shortened_url;
		set => SetProperty(ref is_shortened_url, value);
	}

	public string HostName
	{
		get => host_name;
		set => SetProperty(ref host_name, value);
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

	private string underlying_target_url;
	private bool is_shortened_url;
	private string host_name;
	private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false });
}