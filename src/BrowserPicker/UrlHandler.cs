using BrowserPicker.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Text.RegularExpressions;



#if DEBUG
using JetBrains.Annotations;
#endif

namespace BrowserPicker;

public sealed class UrlHandler : ModelBase, ILongRunningProcess
{
	public UrlHandler(string? requestedUrl, bool disableLookup)
	{
		disallow_network = disableLookup;
		TargetURL = requestedUrl;
		underlying_target_url = requestedUrl;

		if (requestedUrl == null)
		{
			return;
		}
		try
		{
			uri = new Uri(requestedUrl);
			host_name = uri.Host;
		}
		catch
		{
			host_name = string.Empty;
			// ignored
		}
	}

#if DEBUG
	[UsedImplicitly]
	// Design time constructor
	public UrlHandler()
	{
		disallow_network = true;
		TargetURL = "https://www.github.com/mortenn/BrowserPicker";
		uri = new Uri(TargetURL);
		host_name = uri.Host;
		HostName = "extremely-long-domain-example-for-design-time-use.some-long-domain-name.com";
	}
#endif

	/// <summary>
	/// Perform some tests on the Target URL to see if it is a URL shortener
	/// </summary>
	public async Task Start(CancellationToken cancellationToken)
	{
		try
		{
			if (uri == null)
			{
				return;
			}
			HostName = uri.IsFile && !uri.IsUnc ? null : uri.Host;
			while (true)
			{
				var jump = ResolveJumpPage(uri);
				if (jump != null)
				{
					UnderlyingTargetURL = jump;
					uri = new Uri(jump);
					HostName = uri.IsFile && !uri.IsUnc ? null : uri.Host;
					continue;
				}

				if (disallow_network)
					break;

				var shortened = await ResolveShortener(uri, cancellationToken);
				if (shortened != null)
				{
					IsShortenedURL = true;
					UnderlyingTargetURL = shortened;
					uri = new Uri(shortened);
					HostName = uri.IsFile && !uri.IsUnc ? null : uri.Host;
					continue;
				}

				var application = await ResolveApplication(uri, cancellationToken);

				await FindIcon(cancellationToken);
				break;
			}
		}
		catch (TaskCanceledException)
		{
			// TaskCanceledException occurs when the CancellationToken is triggered before the request completes
			// In this case, end the lookup to avoid poor user experience
		}
	}

	private async Task FindIcon(CancellationToken cancellationToken)
	{
		var timeout = new CancellationTokenSource(2000);
		await using var _ = cancellationToken.Register(timeout.Cancel);
		try
		{
			var pageUri = new Uri(underlying_target_url ?? TargetURL ?? "about:blank");
			if (pageUri.IsFile)
			{
				return;
			}
			var result = await Client.GetAsync(pageUri, timeout.Token);
			if (!result.IsSuccessStatusCode)
				return;

			var content = await result.Content.ReadAsStringAsync(timeout.Token);
			var match = Pattern.HtmlLink().Match(content);
			if (!match.Success)
			{
				await TryLoadIcon(new Uri(pageUri, "/favicon.ico").AbsoluteUri, timeout.Token);
				return;
			}
			var link = Pattern.LinkHref().Match(match.Value);
			if (!link.Success)
			{
				return;
			}
			await TryLoadIcon(link.Groups[0].Value, timeout.Token);
		}
		catch (HttpRequestException)
		{
			// ignored
		}
		catch (RegexMatchTimeoutException)
		{
			cancellationToken.ThrowIfCancellationRequested();
		}
		catch (TaskCanceledException)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
		}
	}

	private async Task TryLoadIcon(string iconUrl, CancellationToken cancellationToken)
	{
		var icon = await Client.GetAsync(iconUrl, cancellationToken);
		FavIcon = await icon.Content.ReadAsByteArrayAsync(cancellationToken);
	}

	private static string? ResolveJumpPage(Uri uri)
	{
		return (
			from jumpPage in JumpPages
			where uri.Host.EndsWith(jumpPage.url) || uri.AbsoluteUri.StartsWith(jumpPage.url)
			let queryStringValues = HttpUtility.ParseQueryString(uri.Query)
			select queryStringValues[jumpPage.parameter]
		).FirstOrDefault(underlyingUrl => underlyingUrl != null);
	}

	private static async Task<string?> ResolveShortener(Uri uri, CancellationToken cancellationToken)
	{
		if (UrlShorteners.All(s => !uri.Host.EndsWith(s)))
		{
			return null;
		}
		var response = await Client.GetAsync(uri, cancellationToken);
		var location = response.Headers.Location;
		return location?.OriginalString;
	}

	private static async Task<string?> ResolveApplication(Uri uri, CancellationToken cancellationToken)
	{
		return null;

		/*if (Applications.All(s => !uri.Host.EndsWith(s)))
		{
		}
		var response = await Client.GetAsync(uri, cancellationToken);
		if (response.Headers.Location != null)
		{
			response = await Client.GetAsync(response.Headers.Location, cancellationToken);
		}
		if (!response.IsSuccessStatusCode)
		{
			return null;
		}
		var content = response.Content.ReadAsStringAsync();
		return null;*/
	}

	public string? GetTargetUrl(bool expandFileUrls)
	{
		if (uri == null)
		{
			return null;
		}
		if (uri.IsFile && expandFileUrls)
		{
			return uri.LocalPath;
		}
		return UnderlyingTargetURL ?? TargetURL;
	}

	public string? TargetURL { get; }

	public string? UnderlyingTargetURL
	{
		get => underlying_target_url;
		set
		{
			if (SetProperty(ref underlying_target_url, value))
			{
				OnPropertyChanged(nameof(DisplayURL));
			}
		}
	}

	public bool IsShortenedURL
	{
		get => is_shortened_url;
		set => SetProperty(ref is_shortened_url, value);
	}

	public string? HostName
	{
		get => host_name;
		set => SetProperty(ref host_name, value);
	}

	public byte[]? FavIcon
	{
		get => fav_icon;
		private set => SetProperty(ref fav_icon, value);
	}

	public string? DisplayURL => UnderlyingTargetURL ?? TargetURL;

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

	private static readonly List<string> Applications =
	[
		/* "slack.com" // Horrible to handle, need custom code, see https://api.slack.com/reference/deep-linking */
	];

	private Uri? uri;
	private string? underlying_target_url;
	private bool is_shortened_url;
	private string? host_name;
	private byte[]? fav_icon;
	private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false });
	private readonly bool disallow_network;
}