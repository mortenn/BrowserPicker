using System.IO;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;

namespace BrowserPicker.Common.Tests;

public class DiagnosticLogRedactorTests
{
	[Theory]
	[InlineData("https://contoso-my.sharepoint.com/personal/doc?x=1&y=2", "https://contoso-my.sharepoint.com/[redacted]")]
	[InlineData("https://example.com", "https://example.com")]
	[InlineData("https://example.com/", "https://example.com")]
	[InlineData("https://user:secret@example.com:8443/a#frag", "https://example.com:8443/[redacted]")]
	[InlineData("http://example.com/#token", "http://example.com/[redacted]")]
	[InlineData("file:///C:/Users/someone/report.pdf", "file://[redacted]")]
	[InlineData("mailto:someone@example.com", "mailto:[redacted]")]
	[InlineData("https://example.com/[redacted]", "https://example.com/[redacted]")]
	public void RedactUrlKeepsOnlySchemeHostAndPort(string url, string expected)
	{
		DiagnosticLogRedactor.RedactUrl(url).Should().Be(expected);
	}

	[Fact]
	public void RedactUrlKeepsContainerNameButReducesWrappedUrl()
	{
		var wrapped = Uri.EscapeDataString("https://contoso-my.sharepoint.com/personal/doc?x=1");

		var result = DiagnosticLogRedactor.RedactUrl($"ext+container:name=Work&url={wrapped}");

		result.Should().Be("ext+container:name=Work&url=https://contoso-my.sharepoint.com/[redacted]");
	}

	[Fact]
	public void RedactHandlesUrlsInsideSentencesAndQuotes()
	{
		const string text =
			"Launching Firefox: firefox.exe \"https://example.com/secret?q=1\", then (https://other.example/x).";

		var result = DiagnosticLogRedactor.Redact(text, includeFullUrls: false, userProfilePath: null);

		result
			.Should()
			.Be(
				"Launching Firefox: firefox.exe \"https://example.com/[redacted]\", then (https://other.example/[redacted])."
			);
	}

	[Fact]
	public void RedactLeavesTextWithoutUrlsAndProfileIdsAlone()
	{
		const string text = "Rule Contains 'example.com' matched, profile container:Work, at 10:30:00";

		DiagnosticLogRedactor.Redact(text, includeFullUrls: false, userProfilePath: null).Should().Be(text);
	}

	[Fact]
	public void RedactReplacesUserProfileEvenWhenKeepingFullUrls()
	{
		const string text = @"Settings at C:\Users\someone\AppData\Local\BrowserPicker, url https://example.com/a?b=c";

		var result = DiagnosticLogRedactor.Redact(text, includeFullUrls: true, userProfilePath: @"C:\Users\someone");

		result.Should().Be(@"Settings at %USERPROFILE%\AppData\Local\BrowserPicker, url https://example.com/a?b=c");
	}
}

public sealed class DiagnosticFileLogTests : IDisposable
{
	private readonly string directory = Path.Combine(
		Path.GetTempPath(),
		"BrowserPickerTests",
		Guid.NewGuid().ToString("N")
	);
	private DateTimeOffset now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

	public void Dispose()
	{
		if (Directory.Exists(directory))
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	private DiagnosticFileLog CreateLog() => new(directory, () => now, userProfilePath: @"C:\Users\someone");

	private string ReadAll() => string.Join("", Directory.GetFiles(directory, "*.log").Select(File.ReadAllText));

	[Fact]
	public void WritesNothingUntilEnabled()
	{
		var log = CreateLog();
		log.Write(LogLevel.Information, "Test", new EventId(1), "before configure", null);

		log.Configure(enable: false, includeFullUrls: false, retentionDays: 1);
		log.Write(LogLevel.Information, "Test", new EventId(1), "after configure", null);

		Directory.Exists(directory).Should().BeFalse();
		log.IsAccepting.Should().BeFalse();
	}

	[Fact]
	public void FlushesEntriesHeldBeforeConfigurationWhenEnabled()
	{
		var log = CreateLog();
		log.Write(LogLevel.Debug, "BrowserPicker.Test", new EventId(7), "held https://example.com/private", null);

		log.Configure(enable: true, includeFullUrls: false, retentionDays: 1);
		log.Write(LogLevel.Information, "BrowserPicker.Test", new EventId(8), "live", null);

		var content = ReadAll();
		content.Should().Contain("[DBG]").And.Contain("Test #7: held https://example.com/[redacted]");
		content.Should().Contain("Test #8: live");
		content.Should().NotContain("private");
	}

	[Fact]
	public void KeepsFullUrlsOnlyWhenAsked()
	{
		var log = CreateLog();
		log.Configure(enable: true, includeFullUrls: true, retentionDays: 1);

		log.Write(LogLevel.Information, "Test", default, "https://example.com/a?b=c", null);

		ReadAll().Should().Contain("https://example.com/a?b=c");
	}

	[Fact]
	public void RedactsExceptions()
	{
		var log = CreateLog();
		log.Configure(enable: true, includeFullUrls: false, retentionDays: 1);

		log.Write(
			LogLevel.Error,
			"Test",
			default,
			"failed",
			new InvalidOperationException("could not open https://example.com/private")
		);

		var content = ReadAll();
		content.Should().Contain("https://example.com/[redacted]");
		content.Should().NotContain("private");
	}

	[Fact]
	public void DeletesFilesOlderThanRetention()
	{
		Directory.CreateDirectory(directory);
		var old = Path.Combine(directory, "browserpicker-20260110.log");
		var recent = Path.Combine(directory, "browserpicker-20260115.log");
		var unrelated = Path.Combine(directory, "other.log");
		foreach (var path in new[] { old, recent, unrelated })
		{
			File.WriteAllText(path, "x");
		}
		File.SetLastWriteTimeUtc(old, now.UtcDateTime.AddDays(-2));
		File.SetLastWriteTimeUtc(recent, now.UtcDateTime.AddHours(-1));
		File.SetLastWriteTimeUtc(unrelated, now.UtcDateTime.AddDays(-30));

		CreateLog().Configure(enable: false, includeFullUrls: false, retentionDays: 1);

		File.Exists(old).Should().BeFalse();
		File.Exists(recent).Should().BeTrue();
		File.Exists(unrelated).Should().BeTrue();
	}

	[Fact]
	public void DeleteAllFilesRemovesEveryLogFile()
	{
		var log = CreateLog();
		log.Configure(enable: true, includeFullUrls: false, retentionDays: 7);
		log.Write(LogLevel.Information, "Test", default, "day one", null);
		now = now.AddDays(1);
		log.Write(LogLevel.Information, "Test", default, "day two", null);
		log.GetLogFiles().Should().HaveCount(2);

		log.DeleteAllFiles().Should().Be(0);

		log.GetLogFiles().Should().BeEmpty();
	}

	[Theory]
	[InlineData(0, DiagnosticFileLog.MinRetentionDays)]
	[InlineData(7, 7)]
	[InlineData(365, DiagnosticFileLog.MaxRetentionDays)]
	public void ClampsRetentionDays(int requested, int expected)
	{
		DiagnosticFileLog.ClampRetentionDays(requested).Should().Be(expected);
	}
}
