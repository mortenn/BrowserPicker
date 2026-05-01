using System.Buffers.Binary;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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

	[Fact]
	public void TlsCertificateSummaryReportsSanHostMatch()
	{
		using var certificate = CreateCertificate("example.com", "example.com", "*.example.org");

		var summary = TlsCertificateSummary.FromCertificate(
			new Uri("https://www.example.org/path"),
			certificate,
			SslPolicyErrors.None,
			[],
			SslProtocols.Tls13,
			TlsCipherSuite.TLS_AES_256_GCM_SHA384
		);

		summary.HostNameMatchesCertificate.Should().BeTrue();
		summary.SubjectAlternativeNames.Should().Equal("example.com", "*.example.org");
		summary.ToDisplayText().Should().Contain("Validation: Valid");
		summary.ToDisplayText().Should().Contain("Host match: Yes (www.example.org)");
		summary.ToDisplayText().Should().Contain("Protocol: Tls13");
		summary.ToDisplayText().Should().Contain("Cipher strength: Strong classical (not post-quantum)");
	}

	[Fact]
	public void TlsCertificateSummaryReportsWeakCipherStrength()
	{
		using var certificate = CreateCertificate("example.com", "example.com");

		var summary = TlsCertificateSummary.FromCertificate(
			new Uri("https://example.com"),
			certificate,
			SslPolicyErrors.None,
			[],
			SslProtocols.Tls12,
			TlsCipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA
		);

		summary.ToDisplayText().Should().Contain("Cipher strength: Weak");
	}

	[Fact]
	public void TlsCertificateSummaryReportsInsecureProtocol()
	{
		using var certificate = CreateCertificate("example.com", "example.com");

		var summary = TlsCertificateSummary.FromCertificate(
			new Uri("https://example.com"),
			certificate,
			SslPolicyErrors.None,
			[],
			(SslProtocols)192,
			TlsCipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA
		);

		summary.ToDisplayText().Should().Contain("Cipher strength: Insecure");
	}

	[Fact]
	public void TlsCertificateSummaryDoesNotOvermatchWildcards()
	{
		using var certificate = CreateCertificate("example.com", "*.example.org");

		var summary = TlsCertificateSummary.FromCertificate(
			new Uri("https://deep.www.example.org/path"),
			certificate,
			SslPolicyErrors.RemoteCertificateNameMismatch,
			["RemoteCertificateNameMismatch"]
		);

		summary.HostNameMatchesCertificate.Should().BeFalse();
		summary.ToDisplayText().Should().Contain("Validation: Problems found");
		summary.ToDisplayText().Should().Contain("Host match: No (deep.www.example.org)");
	}

	[Fact]
	public void TlsCertificateSummaryOnlyAcceptsHttpsTargets()
	{
		var accepted = TlsCertificateSummary.TryCreateTarget("http://example.com", out _, out var errorMessage);

		accepted.Should().BeFalse();
		errorMessage.Should().Contain("only available for HTTPS URLs");
	}

	[Fact]
	public void TlsCertificateSummaryCanSkipCertificateRecords()
	{
		using var certificate = CreateCertificate("example.com", "example.com");

		var summary = TlsCertificateSummary.FromCertificate(
			new Uri("https://example.com"),
			certificate,
			SslPolicyErrors.None,
			[],
			certificateRecordsChecked: false
		);

		summary.ToDisplayText().Should().Contain("CAA / certificate transparency: Not checked");
		summary.ToDisplayText().Should().NotContain("CAA: None");
	}

	[Fact]
	public void TlsCertificateSummaryReportsAlignedCaaRecords()
	{
		using var certificate = CreateCertificate("Let's Encrypt", ["example.com"], includeEmbeddedSct: true);

		var summary = TlsCertificateSummary.FromCertificate(
			new Uri("https://example.com"),
			certificate,
			SslPolicyErrors.None,
			[],
			caaRecords: ["0 issue \"letsencrypt.org\""]
		);

		summary.ToDisplayText().Should().Contain("CAA: Aligned");
		summary.ToDisplayText().Should().NotContain("letsencrypt.org");
		summary.ToDisplayText().Should().Contain("Certificate transparency: Looks normal");
		summary.ToDisplayText().Should().Contain("2 transparency timestamps");
		summary.ToDisplayText().Should().Contain("newest 2024-02-03");
		summary.ToDisplayText().Should().NotContain("sha256_ecdsa");
	}

	[Fact]
	public void TlsCertificateSummaryReportsUnalignedCaaRecords()
	{
		using var certificate = CreateCertificate("Example CA", "example.com");

		var summary = TlsCertificateSummary.FromCertificate(
			new Uri("https://example.com"),
			certificate,
			SslPolicyErrors.None,
			[],
			caaRecords: ["0 issue \"letsencrypt.org\""]
		);

		summary.ToDisplayText().Should().Contain("CAA: Unaligned");
		summary.ToDisplayText().Should().NotContain("letsencrypt.org");
	}

	[Fact]
	public void TlsCertificateSummaryReportsNoCaaRecords()
	{
		using var certificate = CreateCertificate("Example CA", "example.com");

		var summary = TlsCertificateSummary.FromCertificate(
			new Uri("https://example.com"),
			certificate,
			SslPolicyErrors.None,
			[]
		);

		summary.ToDisplayText().Should().Contain("CAA: None");
	}

	private static X509Certificate2 CreateCertificate(string commonName, params string[] dnsNames) =>
		CreateCertificate(commonName, dnsNames, includeEmbeddedSct: false);

	private static X509Certificate2 CreateCertificate(string commonName, string[] dnsNames, bool includeEmbeddedSct)
	{
		using var key = RSA.Create(2048);
		var request = new CertificateRequest(
			$"CN={commonName}",
			key,
			HashAlgorithmName.SHA256,
			RSASignaturePadding.Pkcs1
		);
		var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
		foreach (var dnsName in dnsNames)
		{
			subjectAlternativeNames.AddDnsName(dnsName);
		}
		request.CertificateExtensions.Add(subjectAlternativeNames.Build());
		if (includeEmbeddedSct)
		{
			request.CertificateExtensions.Add(
				new X509Extension("1.3.6.1.4.1.11129.2.4.2", BuildEmbeddedSctExtension(), critical: false)
			);
		}

		return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
	}

	private static byte[] BuildEmbeddedSctExtension()
	{
		var first = BuildSignedCertificateTimestamp(0x11, new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero));
		var second = BuildSignedCertificateTimestamp(0x22, new DateTimeOffset(2024, 2, 3, 4, 5, 6, TimeSpan.Zero));
		var listLength = 2 + first.Length + 2 + second.Length;
		var extension = new byte[2 + listLength];
		BinaryPrimitives.WriteUInt16BigEndian(extension.AsSpan(0, 2), (ushort)listLength);
		var offset = 2;
		WriteSerializedSct(extension, ref offset, first);
		WriteSerializedSct(extension, ref offset, second);
		return extension;
	}

	private static byte[] BuildSignedCertificateTimestamp(byte logIdByte, DateTimeOffset timestamp)
	{
		var sct = new byte[1 + 32 + 8 + 2 + 2 + 2 + 1];
		var offset = 0;
		sct[offset++] = 0;
		sct.AsSpan(offset, 32).Fill(logIdByte);
		offset += 32;
		BinaryPrimitives.WriteUInt64BigEndian(sct.AsSpan(offset, 8), (ulong)timestamp.ToUnixTimeMilliseconds());
		offset += 8;
		BinaryPrimitives.WriteUInt16BigEndian(sct.AsSpan(offset, 2), 0);
		offset += 2;
		sct[offset++] = 4;
		sct[offset++] = 3;
		BinaryPrimitives.WriteUInt16BigEndian(sct.AsSpan(offset, 2), 1);
		offset += 2;
		sct[offset] = 0xff;
		return sct;
	}

	private static void WriteSerializedSct(byte[] extension, ref int offset, byte[] sct)
	{
		BinaryPrimitives.WriteUInt16BigEndian(extension.AsSpan(offset, 2), (ushort)sct.Length);
		offset += 2;
		sct.CopyTo(extension, offset);
		offset += sct.Length;
	}
}
