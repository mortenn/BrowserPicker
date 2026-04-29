using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using BrowserPicker.Common.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
#if DEBUG
using JetBrains.Annotations;
#endif

namespace BrowserPicker.Common;

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
		var canProbe = requestedUrl != null;
		probe_redirects = canProbe && settings.ProbeRedirects;
		redirects_known_only = settings.RedirectsKnownOnly;
		probe_favicons = canProbe && settings.ProbeFavicons;
		favicons_for_defaults = settings.FaviconsForDefaults;
		logger.LogNetworkAccessDisabled(!probe_redirects && !probe_favicons);
		url_shorteners = [.. settings.UrlShorteners];
		defaults = [.. settings.Defaults];

		// Add new ones to config as requested
		var newShorteners = DefaultUrlShorteners.Except(url_shorteners).ToArray();
		if (newShorteners.Length > 0)
		{
			settings.UrlShorteners = [.. settings.UrlShorteners, .. newShorteners];
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
			host_name = uri.IsFile ? null : uri.Host;
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
		probe_redirects = false;
		redirects_known_only = true;
		probe_favicons = false;
		favicons_for_defaults = true;
		url_shorteners = [.. DefaultUrlShorteners];
		defaults = [];
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
			HostName = uri.IsFile ? null : uri.Host;
			while (true)
			{
				var jump = ResolveJumpPage(uri);
				if (jump != null)
				{
					logger.LogJumpUrl(uri);
					UnderlyingTargetURL = jump;
					uri = new Uri(jump);
					HostName = uri.IsFile ? null : uri.Host;
					continue;
				}

				var shortened = await ResolveRedirect(uri, cancellationToken);
				if (shortened != null)
				{
					logger.LogShortenedUrl(shortened);
					IsShortenedURL = true;
					UnderlyingTargetURL = shortened;
					uri = new Uri(shortened);
					HostName = uri.IsFile ? null : uri.Host;
					continue;
				}

				if (!TryGetCurrentPageUri(out var pageUri))
				{
					break;
				}

				if (TryLoadCachedFavicon(pageUri))
				{
					break;
				}

				if (ShouldProbeFavicon(pageUri, probe_favicons, favicons_for_defaults, defaults))
				{
					await FindIcon(pageUri, cancellationToken);
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

	/// <summary>
	/// Re-evaluates favicon policy against the current URL and fetches an icon if the current settings now allow it.
	/// Intended for live settings changes while the picker is already open.
	/// </summary>
	public async Task RefreshFavicon(IApplicationSettings settings, CancellationToken cancellationToken)
	{
		if (FavIconProbed || FavIcon != null || !TryGetCurrentPageUri(out var pageUri))
		{
			return;
		}

		if (TryLoadCachedFavicon(pageUri))
		{
			return;
		}

		if (!ShouldProbeFavicon(pageUri, settings.ProbeFavicons, settings.FaviconsForDefaults, settings.Defaults))
		{
			return;
		}

		await favicon_refresh_lock.WaitAsync(cancellationToken);
		try
		{
			if (FavIconProbed || FavIcon != null || !TryGetCurrentPageUri(out pageUri))
			{
				return;
			}

			if (TryLoadCachedFavicon(pageUri))
			{
				return;
			}

			if (!ShouldProbeFavicon(pageUri, settings.ProbeFavicons, settings.FaviconsForDefaults, settings.Defaults))
			{
				return;
			}

			await FindIcon(pageUri, cancellationToken);
		}
		finally
		{
			favicon_refresh_lock.Release();
		}
	}

	private static bool TryGetCurrentPageUri(string? currentUrl, out Uri pageUri)
	{
		if (Uri.TryCreate(currentUrl, UriKind.Absolute, out var parsed))
		{
			pageUri = parsed;
			return true;
		}

		pageUri = null!;
		return false;
	}

	private bool TryGetCurrentPageUri(out Uri pageUri)
	{
		return TryGetCurrentPageUri(underlying_target_url ?? TargetURL, out pageUri);
	}

	private async Task FindIcon(Uri pageUri, CancellationToken cancellationToken)
	{
		var timeout = new CancellationTokenSource(2000);
		await using var _ = cancellationToken.Register(timeout.Cancel);
		try
		{
			if (pageUri.IsFile)
			{
				return;
			}
			// Only http/https can be fetched; other schemes (e.g. ms-windows-store, mailto) are not supported by HttpClient
			if (pageUri.Scheme != Uri.UriSchemeHttp && pageUri.Scheme != Uri.UriSchemeHttps)
			{
				return;
			}
			FavIconProbed = true;
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
			if (bytes.Length > MaxFaviconBytes)
			{
				return;
			}

			logger.LogFaviconLoaded(toFetch.AbsoluteUri);
			FavIcon = bytes;
			SaveFaviconToCache(pageUri, bytes);
			return;
		}
		logger.LogFaviconFailed(icon.StatusCode);
	}

	private bool TryLoadCachedFavicon(Uri pageUri)
	{
		var cachePath = GetFaviconCachePath(pageUri);
		if (cachePath == null || !File.Exists(cachePath))
		{
			return false;
		}

		try
		{
			var bytes = File.ReadAllBytes(cachePath);
			if (bytes.Length == 0 || bytes.Length > MaxFaviconBytes)
			{
				File.Delete(cachePath);
				return false;
			}

			FavIcon = bytes;
			FavIconProbed = true;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static void SaveFaviconToCache(Uri pageUri, byte[] bytes)
	{
		var cachePath = GetFaviconCachePath(pageUri);
		if (cachePath == null || bytes.Length == 0 || bytes.Length > MaxFaviconBytes)
		{
			return;
		}

		try
		{
			var dir = Path.GetDirectoryName(cachePath);
			if (!string.IsNullOrEmpty(dir))
			{
				Directory.CreateDirectory(dir);
			}

			File.WriteAllBytes(cachePath, bytes);
		}
		catch
		{
			// ignored
		}
	}

	private static string? GetFaviconCachePath(Uri pageUri)
	{
		var host = pageUri.IdnHost.Trim().ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(host))
		{
			return null;
		}

		var root = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			nameof(BrowserPicker),
			"favicons"
		);
		return Path.Combine(root, $"{host}.bin");
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

	private static bool ShouldProbeFavicon(
		Uri pageUri,
		bool probeFavicons,
		bool faviconsForDefaults,
		IEnumerable<DefaultSetting> rules
	)
	{
		if (!probeFavicons)
		{
			return false;
		}

		return !faviconsForDefaults || DefaultsMatch(pageUri, rules);
	}

	private static bool DefaultsMatch(Uri url, IEnumerable<DefaultSetting> rules)
	{
		return rules.Any(rule => rule.MatchLength(url) > 0);
	}

	private async Task<string?> ResolveRedirect(Uri shortenerUri, CancellationToken cancellationToken)
	{
		if (!probe_redirects)
		{
			return null;
		}

		if (redirects_known_only && url_shorteners.All(s => !shortenerUri.Host.EndsWith(s)))
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
				OnPropertyChanged(nameof(SecurityPresentation));
				OnPropertyChanged(nameof(RegistrableDomain));
				OnPropertyChanged(nameof(CanRememberRegistrableDomain));
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
	/// Host name of the (possibly resolved) URL; null for file URLs.
	/// </summary>
	public string? HostName
	{
		get => host_name;
		set
		{
			if (SetProperty(ref host_name, value))
			{
				OnPropertyChanged(nameof(CanRememberChoice));
				OnPropertyChanged(nameof(CanRememberRegistrableDomain));
			}
		}
	}

	/// <summary>
	/// True when the current URL has a host that can be stored as a hostname default.
	/// </summary>
	public bool CanRememberChoice => !string.IsNullOrWhiteSpace(HostName);

	/// <summary>
	/// Registrable domain derived from the current display URL, if it can be classified locally.
	/// </summary>
	public string? RegistrableDomain => SecurityPresentation.RegistrableDomain;

	/// <summary>
	/// True when the registrable domain is a useful broader default than the full host.
	/// </summary>
	public bool CanRememberRegistrableDomain =>
		!string.IsNullOrWhiteSpace(RegistrableDomain)
		&& !string.Equals(RegistrableDomain, HostName, StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Favicon image bytes loaded from the target page, if available.
	/// </summary>
	public byte[]? FavIcon
	{
		get => fav_icon;
		private set => SetProperty(ref fav_icon, value);
	}

	/// <summary>
	/// True when a network favicon probe was attempted for the current URL.
	/// </summary>
	public bool FavIconProbed
	{
		get => fav_icon_probed;
		private set => SetProperty(ref fav_icon_probed, value);
	}

	/// <summary>
	/// The URL to display in the UI (underlying resolved URL or original target).
	/// </summary>
	public string? DisplayURL => UnderlyingTargetURL ?? TargetURL;

	/// <summary>
	/// Local-only URL presentation hints derived from <see cref="DisplayURL"/>.
	/// </summary>
	public UrlSecurityPresentation SecurityPresentation => UrlSecurityPresentation.FromDisplayUrl(DisplayURL);

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
		"go.microsoft.com",
	];

	private static readonly List<(string url, string parameter)> JumpPages =
	[
		("safelinks.protection.outlook.com", "url"),
		("https://staticsint.teams.cdn.office.net/evergreen-assets/safelinks/", "url"),
		("https://l.facebook.com/l.php", "u"),
	];

	private Uri? uri;
	private string? underlying_target_url;
	private bool is_shortened_url;
	private string? host_name;
	private byte[]? fav_icon;
	private bool fav_icon_probed;
	private readonly List<string> url_shorteners;
	private readonly List<DefaultSetting> defaults;
	private readonly SemaphoreSlim favicon_refresh_lock = new(1, 1);
	private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false });
	private readonly bool probe_redirects;
	private readonly bool redirects_known_only;
	private readonly bool probe_favicons;
	private readonly bool favicons_for_defaults;
}
