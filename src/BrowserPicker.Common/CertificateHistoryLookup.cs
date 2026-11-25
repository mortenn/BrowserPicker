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
/// Looks up the public certificate transparency history of a host name on crt.sh. Only the host name is sent.
/// This is separate from the connection check's own CT check, which reads the proofs embedded in the
/// certificate the website presents and needs no request.
/// </summary>
public sealed class CertificateHistoryLookup : IExternalLookup
{
	public const string Title = "Certificate history";
	public const string Source = "crt.sh";

	private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
	private static readonly TimeSpan ResultLifetime = TimeSpan.FromHours(1);

	// crt.sh can return a large history for busy hosts; give up instead of buffering without limit.
	private static readonly HttpClient SharedClient = new() { MaxResponseContentBufferSize = 16 * 1024 * 1024 };

	private readonly HttpClient client;
	private readonly TimeProvider clock;
	private readonly ConcurrentDictionary<string, (DateTimeOffset Fetched, ExternalLookupResult Result)> results = new(
		StringComparer.OrdinalIgnoreCase
	);

	public CertificateHistoryLookup()
		: this(SharedClient, TimeProvider.System) { }

	public CertificateHistoryLookup(HttpClient client, TimeProvider clock)
	{
		this.client = client;
		this.clock = clock;
	}

	/// <summary>
	/// The instance used by the app, so results are cached for the whole session.
	/// </summary>
	public static CertificateHistoryLookup Shared { get; } = new();

	public string? DescribeRequest(Uri url)
	{
		var host = GetHost(url);
		return host == null ? null : string.Format(Strings.Lookup_CertificateHistoryDisclosure, host);
	}

	public async Task<ExternalLookupResult> LookupAsync(Uri url, CancellationToken cancellationToken)
	{
		var host = GetHost(url);
		if (host == null)
		{
			return NotAvailable("This address has no host name to look up.");
		}

		if (results.TryGetValue(host, out var cached) && clock.GetUtcNow() - cached.Fetched < ResultLifetime)
		{
			return cached.Result;
		}

		try
		{
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(RequestTimeout);
			var requestUri = new Uri($"https://crt.sh/?q={Uri.EscapeDataString(host)}&output=json");
			using var response = await client.GetAsync(requestUri, timeout.Token).ConfigureAwait(false);
			if (response.StatusCode == HttpStatusCode.TooManyRequests)
			{
				return NotAvailable("crt.sh is limiting requests. Try again later.", host);
			}

			if (!response.IsSuccessStatusCode)
			{
				return NotAvailable(
					$"crt.sh answered {(int)response.StatusCode}. It is often busy; try again later.",
					host
				);
			}

			var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
			var result = Parse(json, host, clock.GetUtcNow());
			results[host] = (clock.GetUtcNow(), result);
			return result;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return NotAvailable("crt.sh did not answer in time. It is often busy; try again later.", host);
		}
		catch (HttpRequestException)
		{
			return NotAvailable("crt.sh could not be reached, or its answer was too large.", host);
		}
		catch (JsonException)
		{
			return NotAvailable("crt.sh returned an unexpected answer.", host);
		}
	}

	/// <summary>
	/// The host name sent to crt.sh, or null for IP addresses, single-label hosts and non-web URLs.
	/// </summary>
	public static string? GetHost(Uri url)
	{
		if (
			!url.IsAbsoluteUri
			|| url.HostNameType != UriHostNameType.Dns
			|| (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
		)
		{
			return null;
		}

		var host = url.IdnHost.TrimEnd('.').ToLowerInvariant();
		return host.Contains('.') ? host : null;
	}

	/// <summary>
	/// Summarizes a crt.sh JSON answer. A certificate is logged as a precertificate and again as the final
	/// certificate, so entries are counted once per issuer and serial number.
	/// </summary>
	public static ExternalLookupResult Parse(string json, string host, DateTimeOffset now)
	{
		using var document = JsonDocument.Parse(json);
		if (document.RootElement.ValueKind != JsonValueKind.Array)
		{
			throw new JsonException("Expected an array.");
		}

		var certificates = new Dictionary<string, (DateTimeOffset NotBefore, string Issuer)>(StringComparer.Ordinal);
		foreach (var entry in document.RootElement.EnumerateArray())
		{
			if (
				entry.ValueKind != JsonValueKind.Object
				|| !TryGetString(entry, "not_before", out var notBeforeText)
				|| !DateTimeOffset.TryParse(
					notBeforeText,
					CultureInfo.InvariantCulture,
					DateTimeStyles.AssumeUniversal,
					out var notBefore
				)
			)
			{
				continue;
			}

			var issuer = TryGetString(entry, "issuer_name", out var issuerName) ? issuerName : string.Empty;
			var serial =
				TryGetString(entry, "serial_number", out var serialNumber) ? serialNumber
				: entry.TryGetProperty("id", out var id) ? id.ToString()
				: notBeforeText;
			certificates.TryAdd(issuer + "|" + serial, (notBefore, issuer));
		}

		if (certificates.Count == 0)
		{
			return new ExternalLookupResult(
				Title,
				"No certificates found",
				[
					new("Host", host),
					new(
						"Result",
						"No logged certificate names this host. Sites covered only by a wildcard certificate (such as *.example.com) are not listed under their own name."
					),
					new("Source", Source),
				]
			);
		}

		var first = certificates.Values.Min(c => c.NotBefore);
		var latest = certificates.Values.Max(c => c.NotBefore);
		var issuers = certificates
			.Values.GroupBy(c => GetCommonName(c.Issuer))
			.OrderByDescending(g => g.Count())
			.Select(g => g.Key)
			.Where(name => name.Length > 0)
			.ToArray();

		return new ExternalLookupResult(
			Title,
			$"First certificate {RdapDomainLookup.FormatAge(now - first)} ago",
			[
				new("Host", host),
				new("Certificates", certificates.Count.ToString(CultureInfo.CurrentCulture)),
				new("First issued", $"{FormatDate(first)} ({RdapDomainLookup.FormatAge(now - first)} ago)"),
				new("Latest issued", $"{FormatDate(latest)} ({RdapDomainLookup.FormatAge(now - latest)} ago)"),
				new(
					"Issuers",
					issuers.Length == 0
						? null
						: string.Join(", ", issuers.Take(3))
							+ (issuers.Length > 3 ? $" and {issuers.Length - 3} more" : "")
				),
				new("Source", Source),
			]
		);
	}

	/// <summary>
	/// The CN of a distinguished name such as "C=US, O=Let's Encrypt, CN=R3", or the whole name if it has none.
	/// </summary>
	public static string GetCommonName(string distinguishedName)
	{
		foreach (var part in distinguishedName.Split(','))
		{
			var trimmed = part.Trim();
			if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
			{
				return trimmed[3..].Trim('"', ' ');
			}
		}
		return distinguishedName.Trim();
	}

	private static bool TryGetString(JsonElement element, string name, out string value)
	{
		if (element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
		{
			value = property.GetString() ?? string.Empty;
			return value.Length > 0;
		}
		value = string.Empty;
		return false;
	}

	private static string FormatDate(DateTimeOffset date) =>
		date.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture);

	private static ExternalLookupResult NotAvailable(string reason, string? host = null) =>
		new(Title, "Not available", [new("Host", host), new("Result", reason), new("Source", Source)]);
}
