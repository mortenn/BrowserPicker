using AwesomeAssertions;

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

		presentation.SchemeState.Should().Be(expectedSchemeState);
		presentation
			.Segments.Should()
			.Contain(segment =>
				segment.Kind == UrlDisplaySegmentKind.RegistrableDomain && segment.Text == expectedDomain.Split('.')[0]
			);
		presentation.RegistrableDomain.Should().Be(expectedDomain);
		presentation.ToolTip.Should().Contain(expectedDomain);
	}

	[Fact]
	public void FromDisplayUrlBuildsCompactTooltip()
	{
		var presentation = UrlSecurityPresentation.FromDisplayUrl("https://www.github.com/mortenn/BrowserPicker");

		presentation
			.ToolTip.Should()
			.Be(
				string.Join(
					Environment.NewLine,
					"Scheme: HTTPS (secure scheme; TLS was not checked)",
					"Host: www.github.com",
					"Highlighted domain: github.com",
					"Note: No network request was made."
				)
			);
		presentation
			.ToolTipLines.Should()
			.Equal(
				new UrlToolTipLine("Scheme", "HTTPS (secure scheme; TLS was not checked)"),
				new UrlToolTipLine("Host", "www.github.com"),
				new UrlToolTipLine("Highlighted domain", "github.com"),
				new UrlToolTipLine("Note", "No network request was made.")
			);
	}

	[Fact]
	public void FromDisplayUrlBuildsCompactIdnTooltip()
	{
		var presentation = UrlSecurityPresentation.FromDisplayUrl("https://xn--bcher-kva.example/");

		presentation
			.ToolTip.Should()
			.Be(
				string.Join(
					Environment.NewLine,
					"Scheme: HTTPS (secure scheme; TLS was not checked)",
					"Host: bücher.example",
					"Highlighted domain: xn--bcher-kva.example",
					"IDN ASCII: xn--bcher-kva.example",
					"Note: No network request was made."
				)
			);
	}

	[Fact]
	public void FromDisplayUrlBuildsCompactFileTooltip()
	{
		const string url = "file:///c:/windows/win.ini";
		var presentation = UrlSecurityPresentation.FromDisplayUrl(url);

		presentation
			.ToolTip.Should()
			.Be(
				string.Join(
					Environment.NewLine,
					"Scheme: FILE (local scheme)",
					@"Path: C:\windows\win.ini",
					"Note: No network request was made."
				)
			);
	}

	[Fact]
	public void FromDisplayUrlShowsUnicodeAndAsciiForIdn()
	{
		var presentation = UrlSecurityPresentation.FromDisplayUrl("https://xn--bcher-kva.example/");

		presentation.SchemeState.Should().Be(UrlSecuritySchemeState.Secure);
		presentation.Segments.Should().NotContain(segment => segment.Text.Contains("xn--", StringComparison.Ordinal));
		presentation
			.Segments.Should()
			.Contain(segment => segment.Kind == UrlDisplaySegmentKind.NonAsciiHost && segment.Text == "ü");
		string.Concat(presentation.Segments.Select(segment => segment.Text)).Should().Be("https://bücher.example/");
		presentation.ToolTip.Should().Contain("bücher.example");
		presentation.ToolTip.Should().Contain("xn--bcher-kva.example");
		presentation.RegistrableDomain.Should().Be("xn--bcher-kva.example");
	}

	[Fact]
	public void FromDisplayUrlFallsBackForUnclassifiedHost()
	{
		var presentation = UrlSecurityPresentation.FromDisplayUrl("https://example.invalid-tld123/path");

		presentation.Segments.Should().NotContain(segment => segment.Kind == UrlDisplaySegmentKind.RegistrableDomain);
		presentation.RegistrableDomain.Should().BeNull();
		presentation
			.Segments.Should()
			.Contain(segment =>
				segment.Kind == UrlDisplaySegmentKind.Host
				&& segment.Text.Contains("example.invalid-tld123", StringComparison.Ordinal)
			);
	}

	[Fact]
	public void FromDisplayUrlShowsDriveFileUrlsAsWindowsPathsWithEmphasizedDrive()
	{
		const string url = "file:///c:/windows/win.ini";
		var presentation = UrlSecurityPresentation.FromDisplayUrl(url);

		presentation.SchemeState.Should().Be(UrlSecuritySchemeState.Neutral);
		presentation
			.Segments.Should()
			.Equal([
				new UrlDisplaySegment("C:", UrlDisplaySegmentKind.FileRoot),
				new UrlDisplaySegment("\\windows\\", UrlDisplaySegmentKind.Path),
				new UrlDisplaySegment("win.ini", UrlDisplaySegmentKind.FileName),
			]);
		presentation.ToolTip.Should().Contain(@"Path: C:\windows\win.ini");
	}

	[Fact]
	public void FromDisplayUrlShowsUncFileUrlsAsWindowsPaths()
	{
		const string url = "file://server/share/file.txt";
		var presentation = UrlSecurityPresentation.FromDisplayUrl(url);

		presentation.SchemeState.Should().Be(UrlSecuritySchemeState.Neutral);
		presentation
			.Segments.Should()
			.Equal([
				new UrlDisplaySegment(@"\\server", UrlDisplaySegmentKind.FileRoot),
				new UrlDisplaySegment(@"\share\", UrlDisplaySegmentKind.Path),
				new UrlDisplaySegment("file.txt", UrlDisplaySegmentKind.FileName),
			]);
		presentation.ToolTip.Should().Contain(@"Path: \\server\share\file.txt");
	}

	[Fact]
	public void FromDisplayUrlSegmentsNonFileUrlsWithoutAuthorityHost()
	{
		var presentation = UrlSecurityPresentation.FromDisplayUrl("mailto:user@example.com");

		presentation.SchemeState.Should().Be(UrlSecuritySchemeState.Neutral);
		presentation
			.Segments.Should()
			.Contain(segment => segment.Kind == UrlDisplaySegmentKind.Scheme && segment.Text == "mailto:");
		presentation
			.Segments.Should()
			.Contain(segment => segment.Kind == UrlDisplaySegmentKind.Path && segment.Text == "user@example.com");
	}

	[Fact]
	public void HostnameDefaultsDoNotMatchFileUrls()
	{
		var setting = new DefaultSetting(MatchType.Hostname, "company.com", "browser");
		var fileUrl = new Uri("file://server.company.com/share/file.txt");
		var httpsUrl = new Uri("https://server.company.com/path");

		setting.MatchLength(fileUrl).Should().Be(0);
		setting.MatchLength(httpsUrl).Should().Be("company.com".Length);
	}
}
