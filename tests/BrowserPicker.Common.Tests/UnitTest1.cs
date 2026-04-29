namespace BrowserPicker.Common.Tests;

public class UrlSecurityPresentationTests
{
	[Theory]
	[InlineData("https://www.github.com/mortenn/BrowserPicker", UrlSecuritySchemeState.Secure, "github.com")]
	[InlineData("http://example.com/path?x=1#frag", UrlSecuritySchemeState.Insecure, "example.com")]
	[InlineData("https://www.bbc.co.uk/news", UrlSecuritySchemeState.Secure, "bbc.co.uk")]
	public void FromDisplayUrlHighlightsRegistrableDomain(
		string url,
		UrlSecuritySchemeState expectedSchemeState,
		string expectedDomain
	)
	{
		var presentation = UrlSecurityPresentation.FromDisplayUrl(url);

		Assert.Equal(expectedSchemeState, presentation.SchemeState);
		Assert.Contains(
			presentation.Segments,
			segment =>
				segment.Kind == UrlDisplaySegmentKind.RegistrableDomain && segment.Text == expectedDomain.Split('.')[0]
		);
		Assert.Equal(expectedDomain, presentation.RegistrableDomain);
		Assert.Contains(expectedDomain, presentation.ToolTip);
	}

	[Fact]
	public void FromDisplayUrlShowsUnicodeAndAsciiForIdn()
	{
		var presentation = UrlSecurityPresentation.FromDisplayUrl("https://xn--bcher-kva.example/");

		Assert.Equal(UrlSecuritySchemeState.Secure, presentation.SchemeState);
		Assert.DoesNotContain(
			presentation.Segments,
			segment => segment.Text.Contains("xn--", StringComparison.Ordinal)
		);
		Assert.Contains(
			presentation.Segments,
			segment => segment.Kind == UrlDisplaySegmentKind.NonAsciiHost && segment.Text == "ü"
		);
		Assert.Equal("https://bücher.example/", string.Concat(presentation.Segments.Select(segment => segment.Text)));
		Assert.Contains("bücher.example", presentation.ToolTip);
		Assert.Contains("xn--bcher-kva.example", presentation.ToolTip);
		Assert.Equal("xn--bcher-kva.example", presentation.RegistrableDomain);
	}

	[Fact]
	public void FromDisplayUrlFallsBackForUnclassifiedHost()
	{
		var presentation = UrlSecurityPresentation.FromDisplayUrl("https://example.invalid-tld123/path");

		Assert.DoesNotContain(
			presentation.Segments,
			segment => segment.Kind == UrlDisplaySegmentKind.RegistrableDomain
		);
		Assert.Null(presentation.RegistrableDomain);
		Assert.Contains(
			presentation.Segments,
			segment =>
				segment.Kind == UrlDisplaySegmentKind.Host
				&& segment.Text.Contains("example.invalid-tld123", StringComparison.Ordinal)
		);
	}

	[Fact]
	public void FromDisplayUrlShowsDriveFileUrlsAsWindowsPathsWithEmphasizedDrive()
	{
		const string url = "file:///c:/windows/win.ini";
		var presentation = UrlSecurityPresentation.FromDisplayUrl(url);

		Assert.Equal(UrlSecuritySchemeState.Neutral, presentation.SchemeState);
		Assert.Equal(
			[
				new UrlDisplaySegment("C:", UrlDisplaySegmentKind.FileRoot),
				new UrlDisplaySegment("\\windows\\", UrlDisplaySegmentKind.Path),
				new UrlDisplaySegment("win.ini", UrlDisplaySegmentKind.FileName),
			],
			presentation.Segments
		);
		Assert.Contains($"Original URL: {url}", presentation.ToolTip);
	}

	[Fact]
	public void FromDisplayUrlShowsUncFileUrlsAsWindowsPaths()
	{
		const string url = "file://server/share/file.txt";
		var presentation = UrlSecurityPresentation.FromDisplayUrl(url);

		Assert.Equal(UrlSecuritySchemeState.Neutral, presentation.SchemeState);
		Assert.Equal(
			[
				new UrlDisplaySegment(@"\\server", UrlDisplaySegmentKind.FileRoot),
				new UrlDisplaySegment(@"\share\", UrlDisplaySegmentKind.Path),
				new UrlDisplaySegment("file.txt", UrlDisplaySegmentKind.FileName),
			],
			presentation.Segments
		);
		Assert.Contains($"Original URL: {url}", presentation.ToolTip);
	}

	[Fact]
	public void FromDisplayUrlSegmentsNonFileUrlsWithoutAuthorityHost()
	{
		var presentation = UrlSecurityPresentation.FromDisplayUrl("mailto:user@example.com");

		Assert.Equal(UrlSecuritySchemeState.Neutral, presentation.SchemeState);
		Assert.Contains(
			presentation.Segments,
			segment => segment.Kind == UrlDisplaySegmentKind.Scheme && segment.Text == "mailto:"
		);
		Assert.Contains(
			presentation.Segments,
			segment => segment.Kind == UrlDisplaySegmentKind.Path && segment.Text == "user@example.com"
		);
	}

	[Fact]
	public void HostnameDefaultsDoNotMatchFileUrls()
	{
		var setting = new DefaultSetting(MatchType.Hostname, "company.com", "browser");
		var fileUrl = new Uri("file://server.company.com/share/file.txt");
		var httpsUrl = new Uri("https://server.company.com/path");

		Assert.Equal(0, setting.MatchLength(fileUrl));
		Assert.Equal("company.com".Length, setting.MatchLength(httpsUrl));
	}
}
