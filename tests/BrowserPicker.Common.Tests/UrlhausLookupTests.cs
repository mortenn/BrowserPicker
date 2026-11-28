using System.Net;
using System.Net.Http;

namespace BrowserPicker.Common.Tests;

public class UrlhausLookupTests
{
	// Shape from https://urlhaus-api.abuse.ch/#hostinfo
	private const string Listed = """
		{
		  "query_status": "ok",
		  "urlhaus_reference": "https://urlhaus.abuse.ch/host/bad.example/",
		  "host": "bad.example",
		  "firstseen": "2026-11-01 04:43:06 UTC",
		  "url_count": "3",
		  "blacklists": { "spamhaus_dbl": "abused_legit_malware", "surbl": "listed" },
		  "urls": [
		    { "url_status": "online", "date_added": "2026-11-20 10:00:00 UTC", "threat": "malware_download", "tags": ["exe", "AgentTesla"] },
		    { "url_status": "offline", "date_added": "2026-11-02 10:00:00 UTC", "threat": "malware_download", "tags": ["exe"] },
		    { "url_status": "offline", "date_added": "2026-11-01 10:00:00 UTC", "threat": "malware_download", "tags": null }
		  ]
		}
		""";

	private static readonly DateTimeOffset Now = new(2026, 11, 25, 0, 0, 0, TimeSpan.Zero);

	[Fact]
	public void ReadsAListedHost()
	{
		var result = UrlhausLookup.Parse(Listed, "bad.example");
		var rows = result.Details.ToDictionary(row => row.Key, row => row.Value);

		Assert.Equal("Listed: serving malware now", result.Summary);
		Assert.Equal("3", rows["Malware URLs"]);
		Assert.Equal("1", rows["Online now"]);
		Assert.Equal("exe, AgentTesla", rows["Tags"]);
		Assert.Equal("abused_legit_malware", rows["Spamhaus DBL"]);
		Assert.Equal("listed", rows["SURBL"]);
		Assert.Equal("https://urlhaus.abuse.ch/host/bad.example/", rows["Reference"]);
		Assert.NotNull(rows["First seen"]);
		Assert.NotNull(rows["Latest added"]);
	}

	[Fact]
	public void OfflineOnlyHostIsStillListed()
	{
		var json = Listed.Replace("\"online\"", "\"offline\"");

		Assert.Equal("Listed: malware seen on this host", UrlhausLookup.Parse(json, "bad.example").Summary);
	}

	[Fact]
	public void NoResultsSaysItIsNotASafetyVerdict()
	{
		var result = UrlhausLookup.Parse("""{ "query_status": "no_results" }""", "www.example.com");

		Assert.Equal("Not listed", result.Summary);
		Assert.Contains(result.Details, row => row.Value?.Contains("doesn't mean the site is safe") == true);
	}

	[Theory]
	[InlineData("""{ "query_status": "invalid_host" }""")]
	[InlineData("""{ "query_status": "something_new" }""")]
	public void OtherStatusesAreNeutral(string json)
	{
		Assert.Equal("Not available", UrlhausLookup.Parse(json, "www.example.com").Summary);
	}

	[Theory]
	[InlineData("https://www.example.com/account?token=secret", "www.example.com")]
	[InlineData("http://45.61.49.78/razor/r4z0r.mips", "45.61.49.78")]
	[InlineData("https://intranet/", null)]
	[InlineData("https://[::1]/", null)]
	[InlineData("mailto:someone@example.com", null)]
	public void SendsOnlyTheHost(string url, string? expected)
	{
		Assert.Equal(expected, UrlhausLookup.GetHost(new Uri(url)));
	}

	[Fact]
	public async Task PostsTheHostWithTheAuthKeyAndCaches()
	{
		var handler = new FakeHandler(_ => Respond(HttpStatusCode.OK, Listed));
		var lookup = new UrlhausLookup(() => " my-key ", new HttpClient(handler), new FixedClock(Now));
		var token = TestContext.Current.CancellationToken;

		var result = await lookup.LookupAsync(new Uri("https://bad.example/payload.exe?id=secret"), token);
		await lookup.LookupAsync(new Uri("https://bad.example/other"), token);

		Assert.Equal("Listed: serving malware now", result.Summary);
		var request = Assert.Single(handler.Requests);
		Assert.Equal("POST https://urlhaus-api.abuse.ch/v1/host/", request.Line);
		Assert.Equal("host=bad.example", request.Body);
		Assert.Equal("my-key", request.AuthKey);
	}

	[Fact]
	public async Task WithoutAKeyNothingIsSent()
	{
		var handler = new FakeHandler(_ => Respond(HttpStatusCode.OK, Listed));
		var lookup = new UrlhausLookup(() => null, new HttpClient(handler), new FixedClock(Now));

		var result = await lookup.LookupAsync(new Uri("https://bad.example/"), TestContext.Current.CancellationToken);

		Assert.Equal("Not available", result.Summary);
		Assert.Contains(result.Details, row => row.Value?.Contains("Auth-Key") == true);
		Assert.Empty(handler.Requests);
	}

	[Theory]
	[InlineData(HttpStatusCode.Unauthorized, "URLhaus did not accept the saved Auth-Key.")]
	[InlineData(HttpStatusCode.Forbidden, "URLhaus did not accept the saved Auth-Key.")]
	[InlineData(HttpStatusCode.TooManyRequests, "URLhaus is limiting requests. Try again later.")]
	[InlineData(HttpStatusCode.ServiceUnavailable, "URLhaus answered 503.")]
	public async Task ProviderErrorsAreNeutralAndNotCached(HttpStatusCode status, string reason)
	{
		var handler = new FakeHandler(_ => Respond(status, """{ "query_status": "unknown_auth_key" }"""));
		var lookup = new UrlhausLookup(() => "key", new HttpClient(handler), new FixedClock(Now));
		var token = TestContext.Current.CancellationToken;

		var result = await lookup.LookupAsync(new Uri("https://www.example.com/"), token);
		await lookup.LookupAsync(new Uri("https://www.example.com/"), token);

		Assert.Equal("Not available", result.Summary);
		Assert.Contains(result.Details, row => row.Value == reason);
		Assert.Equal(2, handler.Requests.Count);
	}

	[Fact]
	public async Task MalformedOrUnreachableIsNeutral()
	{
		var token = TestContext.Current.CancellationToken;
		var malformed = await new UrlhausLookup(
			() => "key",
			new HttpClient(new FakeHandler(_ => Respond(HttpStatusCode.OK, "<html>"))),
			new FixedClock(Now)
		).LookupAsync(new Uri("https://www.example.com/"), token);
		var unreachable = await new UrlhausLookup(
			() => "key",
			new HttpClient(new FakeHandler(_ => throw new HttpRequestException("offline"))),
			new FixedClock(Now)
		).LookupAsync(new Uri("https://www.example.com/"), token);

		Assert.Equal("Not available", malformed.Summary);
		Assert.Equal("Not available", unreachable.Summary);
	}

	[Fact]
	public void DisclosesTheHostAndKeyUseButNotTheKey()
	{
		var text = new UrlhausLookup(() => "super-secret-key").DescribeRequest(
			new Uri("https://www.example.com/path?q=secret")
		);

		Assert.NotNull(text);
		Assert.Contains("\"www.example.com\"", text);
		Assert.Contains("Auth-Key", text);
		Assert.DoesNotContain("secret", text);
	}

	private static HttpResponseMessage Respond(HttpStatusCode status, string body) =>
		new(status) { Content = new StringContent(body) };

	private sealed record RecordedRequest(string Line, string Body, string? AuthKey);

	private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
	{
		public List<RecordedRequest> Requests { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken
		)
		{
			var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
			Requests.Add(
				new RecordedRequest(
					$"{request.Method} {request.RequestUri}",
					body,
					request.Headers.TryGetValues("Auth-Key", out var keys) ? keys.Single() : null
				)
			);
			return respond(request);
		}
	}

	private sealed class FixedClock(DateTimeOffset now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => now;
	}
}
