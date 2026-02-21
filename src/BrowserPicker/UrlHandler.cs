using BrowserPicker.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


#if DEBUG
using JetBrains.Annotations;
#endif

namespace BrowserPicker;

/// <summary>
/// Handles the target URL: resolves shorteners, jump pages, and optional favicon loading.
/// </summary>
public sealed class UrlHandler : ModelBase, ILongRunningProcess
{
	private readonly ILogger logger;

	/// <summary>
	/// Initializes the URL handler with the requested URL and settings.
	/// </summary>
	/// <param name="logger">Logger for URL resolution and favicon steps.</param>
	/// <param name="requestedUrl">The URL passed to the application (e.g. from command line).</param>
	/// <param name="settings">Application settings (shorteners, network access).</param>
	public UrlHandler(ILogger<UrlHandler> logger, string? requestedUrl, IApplicationSettings settings)
	{
		this.logger = logger;
		logger.LogRequestedUrl(requestedUrl);
		disallow_network = requestedUrl == null || settings.DisableNetworkAccess;
		logger.LogNetworkAccessDisabled(disallow_network);
		url_shorteners = [..settings.UrlShorteners];

		// Add new ones to config as requested
		var newShorteners = DefaultUrlShorteners.Except(url_shorteners).ToArray();
		if (newShorteners.Length > 0)
		{
			settings.UrlShorteners = [..settings.UrlShorteners, ..newShorteners];
		}
		
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
		logger = NullLogger.Instance;
		disallow_network = true;
		url_shorteners = [..DefaultUrlShorteners];
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
					logger.LogJumpUrl(uri);
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
					logger.LogShortenedUrl(shortened);
					IsShortenedURL = true;
					UnderlyingTargetURL = shortened;
					uri = new Uri(shortened);
					HostName = uri.IsFile && !uri.IsUnc ? null : uri.Host;
					continue;
				}

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
			// Only http/https can be fetched; other schemes (e.g. ms-windows-store, mailto) are not supported by HttpClient
			if (pageUri.Scheme != Uri.UriSchemeHttp && pageUri.Scheme != Uri.UriSchemeHttps)
			{
				return;
			}
			var result = await Client.GetAsync(pageUri, timeout.Token);
			if (!result.IsSuccessStatusCode)
			{
				logger.LogFaviconFailed(result.StatusCode);
				return;
			}

			// Read as bytes and decode as UTF-8 to avoid InvalidOperationException when the server
			// sends an unsupported charset (e.g. UTF-8-SIG from Discord CDN).
			var bytes = await result.Content.ReadAsByteArrayAsync(timeout.Token);
			var content = Encoding.UTF8.GetString(bytes);
			var match = Pattern.HtmlLink().Match(content);
			if (!match.Success)
			{
				logger.LogDefaultFavicon();
				await TryLoadIcon(new Uri(pageUri, "/favicon.ico"), pageUri, timeout.Token);
				return;
			}
			var link = Pattern.LinkHref().Match(match.Value);
			if (!link.Success)
			{
				logger.LogFaviconNotFound();
				return;
			}

			var href = link.Groups[0].Value.Trim();
			logger.LogFaviconFound(href);
			if (!Uri.TryCreate(pageUri, href, out var iconUri) || !IsSafeFaviconUri(iconUri, pageUri))
			{
				logger.LogDefaultFavicon();
				await TryLoadIcon(new Uri(pageUri, "/favicon.ico"), pageUri, timeout.Token);
				return;
			}
			await TryLoadIcon(iconUri, pageUri, timeout.Token);
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

	/// <summary>
	/// Only allow http/https and same host as the page to prevent SSRF and protocol abuse.
	/// Caller must pass an absolute URI (resolve relative against page first).
	/// </summary>
	private static bool IsSafeFaviconUri(Uri iconUri, Uri pageUri)
	{
		if (iconUri.Scheme != Uri.UriSchemeHttp && iconUri.Scheme != Uri.UriSchemeHttps)
			return false;
		return string.Equals(iconUri.Host, pageUri.Host, StringComparison.OrdinalIgnoreCase);
	}

	private const int MaxFaviconBytes = 512 * 1024;

	private async Task TryLoadIcon(Uri iconUri, Uri pageUri, CancellationToken cancellationToken)
	{
		// Resolve relative favicon URL against the page (same-origin by definition).
		var toFetch = iconUri.IsAbsoluteUri ? iconUri : new Uri(pageUri, iconUri);
		if (!IsSafeFaviconUri(toFetch, pageUri))
			return;
		var icon = await Client.GetAsync(toFetch, cancellationToken);
		if (icon.IsSuccessStatusCode)
		{
			var bytes = await icon.Content.ReadAsByteArrayAsync(cancellationToken);
			if (bytes.Length <= MaxFaviconBytes)
			{
				logger.LogFaviconLoaded(toFetch.AbsoluteUri);
				FavIcon = bytes;
			}
			return;
		}
		logger.LogFaviconFailed(icon.StatusCode);
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

	private async Task<string?> ResolveShortener(Uri shortenerUri, CancellationToken cancellationToken)
	{
		if (url_shorteners.All(s => !shortenerUri.Host.EndsWith(s)))
		{
			return null;
		}
		var response = await Client.GetAsync(shortenerUri, cancellationToken);
		var location = response.Headers.Location;
		return location?.OriginalString;
	}

	/// <summary>
	/// Returns the URL to pass to the browser. For file URLs, may expand to a local path when <paramref name="expandFileUrls"/> is true.
	/// </summary>
	/// <param name="expandFileUrls">When true, file:// URLs are returned as local paths.</param>
	/// <returns>The URL or path to open, or null if none.</returns>
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

	/// <summary>
	/// The URL as originally passed to the application.
	/// </summary>
	public string? TargetURL { get; }

	/// <summary>
	/// The resolved URL after following shorteners or jump pages; same as <see cref="TargetURL"/> if no resolution occurred.
	/// </summary>
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

	/// <summary>
	/// True when the URL was resolved from a known shortener.
	/// </summary>
	public bool IsShortenedURL
	{
		get => is_shortened_url;
		set => SetProperty(ref is_shortened_url, value);
	}

	/// <summary>
	/// Host name of the (possibly resolved) URL; null for file URLs when not UNC.
	/// </summary>
	public string? HostName
	{
		get => host_name;
		set => SetProperty(ref host_name, value);
	}

	/// <summary>
	/// Favicon image bytes loaded from the target page, if available.
	/// </summary>
	public byte[]? FavIcon
	{
		get => fav_icon;
		private set => SetProperty(ref fav_icon, value);
	}

	/// <summary>
	/// The URL to display in the UI (underlying resolved URL or original target).
	/// </summary>
	public string? DisplayURL => UnderlyingTargetURL ?? TargetURL;

	/// <summary>
	/// Default list of URL shortener host names used to resolve redirects.
	/// </summary>
	public static readonly string[] DefaultUrlShorteners =
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

	private Uri? uri;
	private string? underlying_target_url;
	private bool is_shortened_url;
	private string? host_name;
	private byte[]? fav_icon;
	private readonly List<string> url_shorteners;
	private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false });
	private readonly bool disallow_network;
}