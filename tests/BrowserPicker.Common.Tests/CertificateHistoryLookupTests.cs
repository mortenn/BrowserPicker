using System.Net;
using System.Net.Http;

namespace BrowserPicker.Common.Tests;

public class CertificateHistoryLookupTests
{
	// Shape taken from a real crt.sh answer; the first two entries are the precertificate and the
	// final certificate for the same serial number.
	private const string History = """
		[
		  {"issuer_ca_id":204411,"issuer_name":"C=GB, O=Sectigo Limited, CN=Sectigo Public Server Authentication CA OV R36","common_name":"example.com","name_value":"www.example.com","id":2,"not_before":"2026-11-20T00:00:00","not_after":"2027-11-20T23:59:59","serial_number":"aa"},
		  {"issuer_ca_id":204411,"issuer_name":"C=GB, O=Sectigo Limited, CN=Sectigo Public Server Authentication CA OV R36","common_name":"example.com","name_value":"www.example.com","id":3,"not_before":"2026-11-20T00:00:00","not_after":"2027-11-20T23:59:59","serial_number":"aa"},
		  {"issuer_ca_id":1,"issuer_name":"C=US, O=\"thawte, Inc.\", CN=thawte SSL CA - G2","common_name":"www.example.com","name_value":"www.example.com","id":1,"not_before":"2014-11-06T00:00:00","not_after":"2015-11-06T23:59:59","serial_number":"bb"}
		]
		""";

	private static readonly DateTimeOffset Now = new(2026, 11, 25, 0, 0, 0, TimeSpan.Zero);

	[Fact]
	public void SummarizesHistoryCountingEachCertificateOnce()
	{
		var result = CertificateHistoryLookup.Parse(History, "www.example.com", Now);
		var rows = result.Details.ToDictionary(row => row.Key, row => row.Value);

		Assert.Equal("First certificate 12 years ago", result.Summary);
		Assert.Equal("2", rows["Certificates"]);
		Assert.Contains("(5 days ago)", rows["Latest issued"]);
		Assert.Equal("Sectigo Public Server Authentication CA OV R36, thawte SSL CA - G2", rows["Issuers"]);
		Assert.Equal("crt.sh", rows["Source"]);
	}

	[Fact]
	public void NewHostShowsItsAge()
	{
		const string json = """
			[{"issuer_name":"C=US, O=Let's Encrypt, CN=R12","not_before":"2026-11-23T08:00:00","serial_number":"01"}]
			""";

		var result = CertificateHistoryLookup.Parse(json, "login.example.net", Now);

		Assert.Equal("First certificate 1 day ago", result.Summary);
	}

	[Fact]
	public void EmptyHistoryIsNeutralAndMentionsWildcards()
	{
		var result = CertificateHistoryLookup.Parse("[]", "shop.example.com", Now);

		Assert.Equal("No certificates found", result.Summary);
		Assert.Contains(result.Details, row => row.Value?.Contains("wildcard") == true);
	}

	[Theory]
	[InlineData("C=US, O=Let's Encrypt, CN=R3", "R3")]
	[InlineData("C=US, O=\"thawte, Inc.\", CN=thawte SSL CA - G2", "thawte SSL CA - G2")]
	[InlineData("O=No Common Name Ltd", "O=No Common Name Ltd")]
	public void ReadsIssuerCommonName(string issuer, string expected)
	{
		Assert.Equal(expected, CertificateHistoryLookup.GetCommonName(issuer));
	}

	[Theory]
	[InlineData("https://www.example.com/account?token=secret", "www.example.com")]
	[InlineData("https://WWW.Example.COM./", "www.example.com")]
	[InlineData("https://bücher.example/", "xn--bcher-kva.example")]
	[InlineData("https://192.168.1.1/", null)]
	[InlineData("https://intranet/", null)]
	[InlineData("ftp://files.example.com/", null)]
	public void SendsOnlyTheHostName(string url, string? expected)
	{
		Assert.Equal(expected, CertificateHistoryLookup.GetHost(new Uri(url)));
	}

	[Fact]
	public async Task QueriesCrtShWithTheHostOnlyAndCaches()
	{
		var handler = new FakeHandler(_ => Respond(HttpStatusCode.OK, History));
		var lookup = new CertificateHistoryLookup(new HttpClient(handler), new FixedClock(Now));
		var token = TestContext.Current.CancellationToken;

		var result = await lookup.LookupAsync(new Uri("https://www.example.com/account?token=secret"), token);
		await lookup.LookupAsync(new Uri("https://www.example.com/other"), token);

		Assert.Equal("First certificate 12 years ago", result.Summary);
		Assert.Equal(["https://crt.sh/?q=www.example.com&output=json"], handler.Requests);
	}

	[Theory]
	[InlineData(HttpStatusCode.TooManyRequests, "crt.sh is limiting requests. Try again later.")]
	[InlineData(HttpStatusCode.BadGateway, "crt.sh answered 502. It is often busy; try again later.")]
	public async Task ProviderErrorsAreNeutralAndNotCached(HttpStatusCode status, string reason)
	{
		var handler = new FakeHandler(_ => Respond(status, ""));
		var lookup = new CertificateHistoryLookup(new HttpClient(handler), new FixedClock(Now));
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
		var malformed = await new CertificateHistoryLookup(
			new HttpClient(new FakeHandler(_ => Respond(HttpStatusCode.OK, "<html>busy</html>"))),
			new FixedClock(Now)
		).LookupAsync(new Uri("https://www.example.com/"), token);
		var unreachable = await new CertificateHistoryLookup(
			new HttpClient(new FakeHandler(_ => throw new HttpRequestException("offline"))),
			new FixedClock(Now)
		).LookupAsync(new Uri("https://www.example.com/"), token);

		Assert.Equal("Not available", malformed.Summary);
		Assert.Equal("Not available", unreachable.Summary);
	}

	[Fact]
	public void DisclosesTheHostAndProvider()
	{
		var text = new CertificateHistoryLookup().DescribeRequest(new Uri("https://www.example.com/path?q=secret"));

		Assert.NotNull(text);
		Assert.Contains("\"www.example.com\"", text);
		Assert.Contains("crt.sh", text);
		Assert.DoesNotContain("secret", text);
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
