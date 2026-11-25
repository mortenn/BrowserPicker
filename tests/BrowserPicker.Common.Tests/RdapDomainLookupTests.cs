using System.Net;
using System.Net.Http;

namespace BrowserPicker.Common.Tests;

public class RdapDomainLookupTests
{
	private const string Bootstrap = """
		{
		  "version": "1.0",
		  "services": [
		    [["com", "net"], ["http://rdap.example-registry.com/v1/", "https://rdap.example-registry.com/v1/"]],
		    [["uk"], ["https://rdap.nominet.example/uk"]],
		    [["insecure"], ["http://rdap.insecure.example/"]]
		  ]
		}
		""";

	private const string DomainResponse = """
		{
		  "objectClassName": "domain",
		  "ldhName": "EXAMPLE.COM",
		  "status": ["client transfer prohibited", "active"],
		  "events": [
		    { "eventAction": "registration", "eventDate": "2026-11-15T10:00:00Z" },
		    { "eventAction": "expiration", "eventDate": "2027-11-15T10:00:00Z" },
		    { "eventAction": "last changed", "eventDate": "2026-11-16T10:00:00Z" }
		  ],
		  "entities": [
		    { "roles": ["technical"], "vcardArray": ["vcard", [["fn", {}, "text", "Not the registrar"]]] },
		    { "roles": ["registrar"], "handle": "292", "vcardArray": ["vcard", [["version", {}, "text", "4.0"], ["fn", {}, "text", "Example Registrar Inc."]]] }
		  ]
		}
		""";

	private static readonly DateTimeOffset Now = new(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);

	[Fact]
	public void ParsesBootstrapPreferringHttps()
	{
		var services = RdapDomainLookup.ParseBootstrap(Bootstrap);

		Assert.Equal("https://rdap.example-registry.com/v1/", services["com"].ToString());
		Assert.Equal("https://rdap.example-registry.com/v1/", services["net"].ToString());
		Assert.Equal("https://rdap.nominet.example/uk/", services["uk"].ToString());
		Assert.False(services.ContainsKey("insecure"));
	}

	[Theory]
	[InlineData("example.com", "https://rdap.example-registry.com/v1/")]
	[InlineData("example.co.uk", "https://rdap.nominet.example/uk/")]
	[InlineData("example.org", null)]
	public void FindsServiceByLongestSuffix(string domain, string? expected)
	{
		var services = RdapDomainLookup.ParseBootstrap(Bootstrap);

		Assert.Equal(expected, RdapDomainLookup.FindService(services, domain)?.ToString());
	}

	[Theory]
	[InlineData("https://www.example.com/path?q=secret", "example.com")]
	[InlineData("https://login.example.co.uk/", "example.co.uk")]
	[InlineData("https://192.168.1.1/", null)]
	[InlineData("https://localhost/", null)]
	public void SendsOnlyTheRegistrableDomain(string url, string? expected)
	{
		Assert.Equal(expected, RdapDomainLookup.GetDomain(new Uri(url)));
	}

	[Fact]
	public void ParsesRegistrationData()
	{
		var result = RdapDomainLookup.ParseDomain(DomainResponse, "example.com", "rdap.example-registry.com", Now);
		var rows = result.Details.ToDictionary(row => row.Key, row => row.Value);

		Assert.Equal("Registered 10 days ago", result.Summary);
		Assert.Equal("Example Registrar Inc.", rows["Registrar"]);
		Assert.Equal("client transfer prohibited, active", rows["Status"]);
		Assert.Equal("rdap.example-registry.com", rows["Source"]);
		Assert.NotNull(rows["Expires"]);
	}

	[Fact]
	public void MissingFieldsAreLeftEmpty()
	{
		var result = RdapDomainLookup.ParseDomain("{}", "example.com", "rdap.example-registry.com", Now);
		var rows = result.Details.ToDictionary(row => row.Key, row => row.Value);

		Assert.Equal("No registration date", result.Summary);
		Assert.Null(rows["Registrar"]);
		Assert.Null(rows["Registered"]);
	}

	[Theory]
	[InlineData(1, "1 day")]
	[InlineData(45, "45 days")]
	[InlineData(400, "13 months")]
	[InlineData(3650, "10 years")]
	public void FormatsAge(int days, string expected)
	{
		Assert.Equal(expected, RdapDomainLookup.FormatAge(TimeSpan.FromDays(days)));
	}

	[Fact]
	public async Task QueriesTheRegistryWithTheDomainOnly()
	{
		var handler = new FakeHandler(request =>
			request.RequestUri!.Host == "data.iana.org"
				? Respond(HttpStatusCode.OK, Bootstrap)
				: Respond(HttpStatusCode.OK, DomainResponse)
		);
		var lookup = new RdapDomainLookup(new HttpClient(handler), new FixedClock(Now));

		var result = await lookup.LookupAsync(
			new Uri("https://www.example.com/account?token=secret"),
			TestContext.Current.CancellationToken
		);

		Assert.Equal("Registered 10 days ago", result.Summary);
		Assert.Equal(
			["https://data.iana.org/rdap/dns.json", "https://rdap.example-registry.com/v1/domain/example.com"],
			handler.Requests
		);
	}

	[Fact]
	public async Task CachesBootstrapAndResults()
	{
		var handler = new FakeHandler(request =>
			request.RequestUri!.Host == "data.iana.org"
				? Respond(HttpStatusCode.OK, Bootstrap)
				: Respond(HttpStatusCode.OK, DomainResponse)
		);
		var lookup = new RdapDomainLookup(new HttpClient(handler), new FixedClock(Now));
		var token = TestContext.Current.CancellationToken;

		await lookup.LookupAsync(new Uri("https://example.com/"), token);
		await lookup.LookupAsync(new Uri("https://www.example.com/other"), token);
		await lookup.LookupAsync(new Uri("https://example.net/"), token);

		Assert.Equal(3, handler.Requests.Count);
	}

	[Theory]
	[InlineData(HttpStatusCode.NotFound, "The registry has no record of this domain.")]
	[InlineData(HttpStatusCode.TooManyRequests, "The RDAP service is limiting requests. Try again later.")]
	[InlineData(HttpStatusCode.InternalServerError, "The RDAP service answered 500.")]
	public async Task FailuresAreNeutral(HttpStatusCode status, string reason)
	{
		var handler = new FakeHandler(request =>
			request.RequestUri!.Host == "data.iana.org" ? Respond(HttpStatusCode.OK, Bootstrap) : Respond(status, "")
		);
		var lookup = new RdapDomainLookup(new HttpClient(handler), new FixedClock(Now));

		var result = await lookup.LookupAsync(new Uri("https://example.com/"), TestContext.Current.CancellationToken);

		Assert.Equal("Not available", result.Summary);
		Assert.Contains(result.Details, row => row.Value == reason);
	}

	[Fact]
	public async Task UnlistedTldAndMalformedAnswersAreNeutral()
	{
		var handler = new FakeHandler(request =>
			request.RequestUri!.Host == "data.iana.org"
				? Respond(HttpStatusCode.OK, Bootstrap)
				: Respond(HttpStatusCode.OK, "not json")
		);
		var lookup = new RdapDomainLookup(new HttpClient(handler), new FixedClock(Now));
		var token = TestContext.Current.CancellationToken;

		var unlisted = await lookup.LookupAsync(new Uri("https://example.org/"), token);
		var malformed = await lookup.LookupAsync(new Uri("https://example.com/"), token);

		Assert.Equal("Not available", unlisted.Summary);
		Assert.Contains(unlisted.Details, row => row.Value == "No RDAP service is listed for .org.");
		Assert.Equal("Not available", malformed.Summary);
	}

	[Fact]
	public async Task UnreachableServiceIsNeutral()
	{
		var handler = new FakeHandler(_ => throw new HttpRequestException("offline"));
		var lookup = new RdapDomainLookup(new HttpClient(handler), new FixedClock(Now));

		var result = await lookup.LookupAsync(new Uri("https://example.com/"), TestContext.Current.CancellationToken);

		Assert.Equal("Not available", result.Summary);
	}

	[Fact]
	public void DisclosesTheDomainBeforeTheRequest()
	{
		var text = new RdapDomainLookup().DescribeRequest(new Uri("https://www.example.com/path?q=secret"));

		Assert.NotNull(text);
		Assert.Contains("\"example.com\"", text);
		Assert.DoesNotContain("secret", text);
		Assert.Null(new RdapDomainLookup().DescribeRequest(new Uri("https://10.0.0.1/")));
	}

	private static HttpResponseMessage Respond(HttpStatusCode status, string body) =>
		new(status) { Content = new StringContent(body) };

	private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
	{
		public List<string> Requests { get; } = [];

		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken
		)
		{
			Requests.Add(request.RequestUri!.ToString());
			return Task.FromResult(respond(request));
		}
	}

	private sealed class FixedClock(DateTimeOffset now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => now;
	}
}
