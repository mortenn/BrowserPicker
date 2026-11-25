using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BrowserPicker.Common.Resources;

namespace BrowserPicker.Common;

/// <summary>
/// Looks up a host on URLhaus (abuse.ch), which tracks URLs that distribute malware. Uses the user's own
/// Auth-Key and sends only the host name, never the URL. URLhaus doesn't track phishing, so a host that
/// isn't listed is not a verdict that the site is safe.
/// </summary>
public sealed class UrlhausLookup : IExternalLookup
{
	public const string Title = "URLhaus";
	public const string Source = "URLhaus (abuse.ch)";
	public static readonly Uri HostEndpoint = new("https://urlhaus-api.abuse.ch/v1/host/");

	private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan ResultLifetime = TimeSpan.FromHours(1);
	private static readonly HttpClient SharedClient = new();
	private static readonly ConcurrentDictionary<
		string,
		(DateTimeOffset Fetched, ExternalLookupResult Result)
	> Results = new(StringComparer.OrdinalIgnoreCase);

	private readonly Func<string?> auth_key;
	private readonly HttpClient client;
	private readonly TimeProvider clock;
	private readonly ConcurrentDictionary<string, (DateTimeOffset Fetched, ExternalLookupResult Result)> results;

	/// <param name="authKey">Reads the user's Auth-Key when a lookup starts, so a changed key is used at once.</param>
	public UrlhausLookup(Func<string?> authKey)
		: this(authKey, SharedClient, TimeProvider.System, Results) { }

	public UrlhausLookup(Func<string?> authKey, HttpClient client, TimeProvider clock)
		: this(authKey, client, clock, new(StringComparer.OrdinalIgnoreCase)) { }

	private UrlhausLookup(
		Func<string?> authKey,
		HttpClient client,
		TimeProvider clock,
		ConcurrentDictionary<string, (DateTimeOffset Fetched, ExternalLookupResult Result)> results
	)
	{
		auth_key = authKey;
		this.client = client;
		this.clock = clock;
		this.results = results;
	}

	public string? DescribeRequest(Uri url)
	{
		var host = GetHost(url);
		return host == null ? null : string.Format(Strings.Lookup_UrlhausDisclosure, host);
	}

	public async Task<ExternalLookupResult> LookupAsync(Uri url, CancellationToken cancellationToken)
	{
		var host = GetHost(url);
		if (host == null)
		{
			return NotAvailable("This address has no host name to look up.");
		}

		var key = auth_key();
		if (string.IsNullOrWhiteSpace(key))
		{
			return NotAvailable("Save your abuse.ch Auth-Key in Settings > Security to use URLhaus.", host);
		}

		if (results.TryGetValue(host, out var cached) && clock.GetUtcNow() - cached.Fetched < ResultLifetime)
		{
			return cached.Result;
		}

		try
		{
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(RequestTimeout);
			using var request = new HttpRequestMessage(HttpMethod.Post, HostEndpoint)
			{
				Content = new FormUrlEncodedContent([new("host", host)]),
			};
			request.Headers.Add("Auth-Key", key.Trim());
			using var response = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);

			switch (response.StatusCode)
			{
				case HttpStatusCode.Unauthorized:
				case HttpStatusCode.Forbidden:
					return NotAvailable("URLhaus did not accept the saved Auth-Key.", host);
				case HttpStatusCode.TooManyRequests:
					return NotAvailable("URLhaus is limiting requests. Try again later.", host);
			}

			if (!response.IsSuccessStatusCode)
			{
				return NotAvailable($"URLhaus answered {(int)response.StatusCode}.", host);
			}

			var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
			var result = Parse(json, host);
			if (result.Summary != NotAvailableSummary)
			{
				results[host] = (clock.GetUtcNow(), result);
			}
			return result;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return NotAvailable("URLhaus did not answer in time.", host);
		}
		catch (HttpRequestException)
		{
			return NotAvailable("URLhaus could not be reached.", host);
		}
		catch (JsonException)
		{
			return NotAvailable("URLhaus returned an unexpected answer.", host);
		}
	}

	/// <summary>
	/// The host name or IPv4 address sent to URLhaus, or null for other URLs.
	/// </summary>
	public static string? GetHost(Uri url)
	{
		if (!url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
		{
			return null;
		}

		if (url.HostNameType == UriHostNameType.IPv4)
		{
			return url.Host;
		}

		if (url.HostNameType != UriHostNameType.Dns)
		{
			return null;
		}

		var host = url.IdnHost.TrimEnd('.').ToLowerInvariant();
		return host.Contains('.') ? host : null;
	}

	public const string NotAvailableSummary = "Not available";
	public const string NotListedSummary = "Not listed";

	/// <summary>
	/// Reads a URLhaus host answer (https://urlhaus-api.abuse.ch/#hostinfo).
	/// </summary>
	public static ExternalLookupResult Parse(string json, string host)
	{
		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;
		var status = GetString(root, "query_status");
		switch (status)
		{
			case "no_results":
				return new ExternalLookupResult(
					Title,
					NotListedSummary,
					[
						new("Host", host),
						new(
							"Result",
							"URLhaus has no malware URLs for this host. That doesn't mean the site is safe: URLhaus tracks malware downloads, not phishing."
						),
						new("Source", Source),
					]
				);
			case "ok":
				break;
			case "invalid_host":
				return NotAvailable("URLhaus did not accept this host name.", host);
			default:
				return NotAvailable("URLhaus returned an unexpected answer.", host);
		}

		var urls =
			root.TryGetProperty("urls", out var list) && list.ValueKind == JsonValueKind.Array
				? list.EnumerateArray().Where(u => u.ValueKind == JsonValueKind.Object).ToArray()
				: [];
		var online = urls.Count(u => GetString(u, "url_status") == "online");
		var count = GetString(root, "url_count") ?? urls.Length.ToString(CultureInfo.InvariantCulture);
		var latest = urls.Select(u => ParseDate(GetString(u, "date_added"))).Where(d => d != null).Max();
		var tags = urls.SelectMany(u =>
				u.TryGetProperty("tags", out var t) && t.ValueKind == JsonValueKind.Array
					? t.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!)
					: []
			)
			.GroupBy(tag => tag, StringComparer.OrdinalIgnoreCase)
			.OrderByDescending(g => g.Count())
			.Take(5)
			.Select(g => g.Key)
			.ToArray();
		var blacklists =
			root.TryGetProperty("blacklists", out var b) && b.ValueKind == JsonValueKind.Object ? b : default;

		return new ExternalLookupResult(
			Title,
			online > 0 ? "Listed: serving malware now" : "Listed: malware seen on this host",
			[
				new("Host", host),
				new("Malware URLs", count),
				new("Online now", online.ToString(CultureInfo.CurrentCulture)),
				new("First seen", ParseDate(GetString(root, "firstseen")) is { } first ? FormatDate(first) : null),
				new("Latest added", latest is { } last ? FormatDate(last) : null),
				new("Tags", tags.Length == 0 ? null : string.Join(", ", tags)),
				new(
					"Spamhaus DBL",
					blacklists.ValueKind == JsonValueKind.Object ? GetString(blacklists, "spamhaus_dbl") : null
				),
				new("SURBL", blacklists.ValueKind == JsonValueKind.Object ? GetString(blacklists, "surbl") : null),
				new("Reference", GetString(root, "urlhaus_reference")),
				new("Source", Source),
			]
		);
	}

	private static string? GetString(JsonElement element, string name)
	{
		if (!element.TryGetProperty(name, out var value))
		{
			return null;
		}

		return value.ValueKind switch
		{
			JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()) ? null : value.GetString(),
			JsonValueKind.Number => value.GetRawText(),
			_ => null,
		};
	}

	// URLhaus timestamps look like "2019-01-19 04:43:06 UTC".
	private static DateTimeOffset? ParseDate(string? value) =>
		value != null
		&& DateTimeOffset.TryParseExact(
			value.Replace(" UTC", string.Empty, StringComparison.Ordinal),
			"yyyy-MM-dd HH:mm:ss",
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal,
			out var date
		)
			? date
			: null;

	private static string FormatDate(DateTimeOffset date) =>
		date.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture);

	private static ExternalLookupResult NotAvailable(string reason, string? host = null) =>
		new(Title, NotAvailableSummary, [new("Host", host), new("Result", reason), new("Source", Source)]);
}
