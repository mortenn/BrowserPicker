using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BrowserPicker.Common.Resources;

namespace BrowserPicker.Common;

/// <summary>
/// Looks up domain registration data over RDAP (RFC 9083). The authoritative RDAP service for a TLD is found
/// through the IANA bootstrap registry (RFC 9224). Only the registrable domain is sent, never the URL.
/// </summary>
public sealed class RdapDomainLookup : IExternalLookup
{
	public const string Title = "Domain registration";

	public static readonly Uri BootstrapUri = new("https://data.iana.org/rdap/dns.json");

	private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan BootstrapLifetime = TimeSpan.FromHours(24);
	private static readonly TimeSpan ResultLifetime = TimeSpan.FromHours(1);
	private static readonly HttpClient SharedClient = new();

	private readonly HttpClient client;
	private readonly TimeProvider clock;
	private readonly SemaphoreSlim bootstrap_lock = new(1, 1);
	private readonly ConcurrentDictionary<string, (DateTimeOffset Fetched, ExternalLookupResult Result)> results = new(
		StringComparer.OrdinalIgnoreCase
	);
	private (DateTimeOffset Fetched, IReadOnlyDictionary<string, Uri> Services)? bootstrap;

	public RdapDomainLookup()
		: this(SharedClient, TimeProvider.System) { }

	public RdapDomainLookup(HttpClient client, TimeProvider clock)
	{
		this.client = client;
		this.clock = clock;
	}

	/// <summary>
	/// The instance used by the app, so the bootstrap data and results are cached for the whole session.
	/// </summary>
	public static RdapDomainLookup Shared { get; } = new();

	public string? DescribeRequest(Uri url)
	{
		var domain = GetDomain(url);
		return domain == null ? null : string.Format(Strings.Lookup_RdapDisclosure, domain);
	}

	public async Task<ExternalLookupResult> LookupAsync(Uri url, CancellationToken cancellationToken)
	{
		var domain = GetDomain(url);
		if (domain == null)
		{
			return NotAvailable("This address has no registered domain to look up.");
		}

		if (results.TryGetValue(domain, out var cached) && clock.GetUtcNow() - cached.Fetched < ResultLifetime)
		{
			return cached.Result;
		}

		try
		{
			var services = await GetServicesAsync(cancellationToken).ConfigureAwait(false);
			if (services == null)
			{
				return NotAvailable("The IANA RDAP list could not be loaded.");
			}

			var baseUri = FindService(services, domain);
			if (baseUri == null)
			{
				return NotAvailable($"No RDAP service is listed for .{GetTld(domain)}.", domain);
			}

			var result = await QueryDomainAsync(baseUri, domain, cancellationToken).ConfigureAwait(false);
			if (result.Cacheable)
			{
				results[domain] = (clock.GetUtcNow(), result.Result);
			}
			return result.Result;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return NotAvailable("The RDAP service did not answer in time.", domain);
		}
		catch (HttpRequestException)
		{
			return NotAvailable("The RDAP service could not be reached.", domain);
		}
	}

	/// <summary>
	/// The registrable domain sent to RDAP, or null for IP addresses, single-label hosts and non-web URLs.
	/// </summary>
	public static string? GetDomain(Uri url)
	{
		if (!url.IsAbsoluteUri || url.HostNameType != UriHostNameType.Dns)
		{
			return null;
		}

		var domain = UrlSecurityPresentation.FromDisplayUrl(url.AbsoluteUri).RegistrableDomain;
		return string.IsNullOrWhiteSpace(domain) || !domain.Contains('.') ? null : domain.ToLowerInvariant();
	}

	/// <summary>
	/// Reads the IANA bootstrap file into a map from DNS label suffix (usually a TLD) to RDAP base URL.
	/// HTTPS URLs are preferred, and services without one are skipped.
	/// </summary>
	public static IReadOnlyDictionary<string, Uri> ParseBootstrap(string json)
	{
		var map = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
		using var document = JsonDocument.Parse(json);
		if (
			!document.RootElement.TryGetProperty("services", out var services)
			|| services.ValueKind != JsonValueKind.Array
		)
		{
			return map;
		}

		foreach (var service in services.EnumerateArray())
		{
			if (service.ValueKind != JsonValueKind.Array || service.GetArrayLength() < 2)
			{
				continue;
			}

			var url = service[1]
				.EnumerateArray()
				.Where(u => u.ValueKind == JsonValueKind.String)
				.Select(u => u.GetString())
				.FirstOrDefault(u => u != null && u.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
			if (url == null || !Uri.TryCreate(url.EndsWith('/') ? url : url + "/", UriKind.Absolute, out var baseUri))
			{
				continue;
			}

			foreach (var entry in service[0].EnumerateArray())
			{
				if (entry.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(entry.GetString()))
				{
					map.TryAdd(entry.GetString()!.Trim('.'), baseUri);
				}
			}
		}

		return map;
	}

	/// <summary>
	/// Finds the RDAP service for a domain by its longest matching label suffix, as RFC 9224 requires.
	/// </summary>
	public static Uri? FindService(IReadOnlyDictionary<string, Uri> services, string domain)
	{
		var labels = domain.Trim('.').Split('.');
		for (var i = 0; i < labels.Length; i++)
		{
			if (services.TryGetValue(string.Join('.', labels[i..]), out var baseUri))
			{
				return baseUri;
			}
		}
		return null;
	}

	/// <summary>
	/// Turns an RDAP domain response into the rows shown in the connection check.
	/// </summary>
	public static ExternalLookupResult ParseDomain(string json, string domain, string source, DateTimeOffset now)
	{
		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;
		if (root.ValueKind != JsonValueKind.Object)
		{
			return NotAvailable("The RDAP service returned an unexpected answer.", domain, source);
		}

		var events = ReadEvents(root);
		var registered = Find(events, "registration");
		var summary = registered is { } date ? $"Registered {FormatAge(now - date)} ago" : "No registration date";

		return new ExternalLookupResult(
			Title,
			summary,
			[
				new("Domain", domain),
				new("Registered", registered is { } r ? $"{FormatDate(r)} ({FormatAge(now - r)} ago)" : null),
				new("Last changed", Find(events, "last changed") is { } c ? FormatDate(c) : null),
				new("Expires", Find(events, "expiration") is { } e ? FormatDate(e) : null),
				new("Registrar", ReadRegistrar(root)),
				new("Status", ReadStatus(root)),
				new("Source", source),
			]
		);
	}

	/// <summary>
	/// Describes a time span the way people say a domain's age: days, then months, then years.
	/// </summary>
	public static string FormatAge(TimeSpan age)
	{
		var days = Math.Max(0, (int)age.TotalDays);
		if (days < 60)
		{
			return days == 1 ? "1 day" : $"{days} days";
		}

		if (days < 730)
		{
			return $"{days / 30} months";
		}

		return $"{days / 365} years";
	}

	private async Task<IReadOnlyDictionary<string, Uri>?> GetServicesAsync(CancellationToken cancellationToken)
	{
		await bootstrap_lock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (bootstrap is { } current && clock.GetUtcNow() - current.Fetched < BootstrapLifetime)
			{
				return current.Services;
			}

			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(RequestTimeout);
			using var response = await client.GetAsync(BootstrapUri, timeout.Token).ConfigureAwait(false);
			if (!response.IsSuccessStatusCode)
			{
				return null;
			}

			var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
			var services = ParseBootstrap(json);
			if (services.Count == 0)
			{
				return null;
			}

			bootstrap = (clock.GetUtcNow(), services);
			return services;
		}
		catch (JsonException)
		{
			return null;
		}
		finally
		{
			bootstrap_lock.Release();
		}
	}

	private async Task<(ExternalLookupResult Result, bool Cacheable)> QueryDomainAsync(
		Uri baseUri,
		string domain,
		CancellationToken cancellationToken
	)
	{
		var source = baseUri.Host;
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(RequestTimeout);
		using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "domain/" + domain));
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rdap+json"));
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json", 0.9));
		using var response = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);

		switch (response.StatusCode)
		{
			case HttpStatusCode.NotFound:
				return (NotAvailable("The registry has no record of this domain.", domain, source), true);
			case HttpStatusCode.TooManyRequests:
				return (NotAvailable("The RDAP service is limiting requests. Try again later.", domain, source), false);
		}

		if (!response.IsSuccessStatusCode)
		{
			return (NotAvailable($"The RDAP service answered {(int)response.StatusCode}.", domain, source), false);
		}

		var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
		try
		{
			return (ParseDomain(json, domain, source, clock.GetUtcNow()), true);
		}
		catch (JsonException)
		{
			return (NotAvailable("The RDAP service returned an unexpected answer.", domain, source), false);
		}
	}

	private static Dictionary<string, DateTimeOffset> ReadEvents(JsonElement root)
	{
		var events = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
		if (!root.TryGetProperty("events", out var list) || list.ValueKind != JsonValueKind.Array)
		{
			return events;
		}

		foreach (var item in list.EnumerateArray())
		{
			if (
				item.ValueKind == JsonValueKind.Object
				&& item.TryGetProperty("eventAction", out var action)
				&& action.ValueKind == JsonValueKind.String
				&& item.TryGetProperty("eventDate", out var date)
				&& date.ValueKind == JsonValueKind.String
				&& DateTimeOffset.TryParse(
					date.GetString(),
					CultureInfo.InvariantCulture,
					DateTimeStyles.AssumeUniversal,
					out var parsed
				)
			)
			{
				events.TryAdd(action.GetString()!, parsed);
			}
		}

		return events;
	}

	private static DateTimeOffset? Find(Dictionary<string, DateTimeOffset> events, string action) =>
		events.TryGetValue(action, out var date) ? date : null;

	private static string? ReadRegistrar(JsonElement root)
	{
		if (!root.TryGetProperty("entities", out var entities) || entities.ValueKind != JsonValueKind.Array)
		{
			return null;
		}

		foreach (var entity in entities.EnumerateArray())
		{
			if (
				entity.ValueKind != JsonValueKind.Object
				|| !entity.TryGetProperty("roles", out var roles)
				|| roles.ValueKind != JsonValueKind.Array
				|| !roles
					.EnumerateArray()
					.Any(role => role.ValueKind == JsonValueKind.String && role.GetString() == "registrar")
			)
			{
				continue;
			}

			return ReadVCardName(entity)
				?? (
					entity.TryGetProperty("handle", out var handle) && handle.ValueKind == JsonValueKind.String
						? handle.GetString()
						: null
				);
		}

		return null;
	}

	// jCard (RFC 7095): ["vcard", [["fn", {}, "text", "Example Registrar"], ...]]
	private static string? ReadVCardName(JsonElement entity)
	{
		if (
			!entity.TryGetProperty("vcardArray", out var vcard)
			|| vcard.ValueKind != JsonValueKind.Array
			|| vcard.GetArrayLength() < 2
			|| vcard[1].ValueKind != JsonValueKind.Array
		)
		{
			return null;
		}

		foreach (var property in vcard[1].EnumerateArray())
		{
			if (
				property.ValueKind == JsonValueKind.Array
				&& property.GetArrayLength() >= 4
				&& property[0].ValueKind == JsonValueKind.String
				&& property[0].GetString() == "fn"
				&& property[3].ValueKind == JsonValueKind.String
				&& !string.IsNullOrWhiteSpace(property[3].GetString())
			)
			{
				return property[3].GetString();
			}
		}

		return null;
	}

	private static string? ReadStatus(JsonElement root)
	{
		if (!root.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.Array)
		{
			return null;
		}

		var values = status
			.EnumerateArray()
			.Where(s => s.ValueKind == JsonValueKind.String)
			.Select(s => s.GetString())
			.Where(s => !string.IsNullOrWhiteSpace(s))
			.ToArray();
		return values.Length == 0 ? null : string.Join(", ", values);
	}

	private static string FormatDate(DateTimeOffset date) =>
		date.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture);

	private static string GetTld(string domain) => domain[(domain.LastIndexOf('.') + 1)..];

	private static ExternalLookupResult NotAvailable(string reason, string? domain = null, string? source = null) =>
		new(Title, "Not available", [new("Domain", domain), new("Result", reason), new("Source", source)]);
}
