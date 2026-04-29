using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;

namespace BrowserPicker.Common;

public enum UrlSecuritySchemeState
{
	Secure,
	Insecure,
	Neutral,
}

public enum UrlDisplaySegmentKind
{
	Other,
	Scheme,
	Host,
	RegistrableDomain,
	NonAsciiHost,
	FileRoot,
	FileName,
	Path,
	Query,
	Fragment,
}

public sealed record UrlDisplaySegment(string Text, UrlDisplaySegmentKind Kind);

public sealed record UrlToolTipLine(string Label, string Value);

public sealed record UrlSecurityPresentation(
	IReadOnlyList<UrlDisplaySegment> Segments,
	string SchemeLabel,
	UrlSecuritySchemeState SchemeState,
	string SchemeToolTip,
	string ToolTip,
	IReadOnlyList<UrlToolTipLine> ToolTipLines,
	string? RegistrableDomain
)
{
	private static readonly IdnMapping Idn = new();

	private static readonly (string Suffix, int LabelCount)[] KnownMultiPartSuffixes =
	[
		("co.uk", 2),
		("org.uk", 2),
		("ac.uk", 2),
		("gov.uk", 2),
		("com.au", 2),
		("net.au", 2),
		("org.au", 2),
		("co.nz", 2),
		("com.br", 2),
		("com.tr", 2),
		("co.jp", 2),
	];

	public static UrlSecurityPresentation FromDisplayUrl(string? displayUrl)
	{
		if (string.IsNullOrWhiteSpace(displayUrl))
		{
			return new(
				Array.Empty<UrlDisplaySegment>(),
				string.Empty,
				UrlSecuritySchemeState.Neutral,
				"No URL to inspect.",
				string.Empty,
				Array.Empty<UrlToolTipLine>(),
				null
			);
		}

		if (!Uri.TryCreate(displayUrl, UriKind.Absolute, out var uri))
		{
			return new(
				[new UrlDisplaySegment(displayUrl, UrlDisplaySegmentKind.Other)],
				"URL",
				UrlSecuritySchemeState.Neutral,
				"This text is not an absolute URL.",
				displayUrl,
				[new UrlToolTipLine("URL", "This text is not an absolute URL.")],
				null
			);
		}

		var schemeState = GetSchemeState(uri);
		var schemeLabel = uri.Scheme.ToUpperInvariant();
		if (uri.IsFile)
		{
			var filePath = GetCanonicalFilePath(uri);
			var fileToolTipLines = BuildFileToolTipLines(uri, filePath);
			return new(
				BuildFileSegments(filePath),
				schemeLabel,
				schemeState,
				BuildSchemeToolTip(uri, schemeState),
				BuildToolTip(fileToolTipLines),
				fileToolTipLines,
				null
			);
		}

		var segments = BuildSegments(
			displayUrl,
			uri,
			out var registrableDomain,
			out var unicodeHost,
			out var asciiHost
		);
		var schemeToolTip = BuildSchemeToolTip(uri, schemeState);
		var toolTipLines = BuildToolTipLines(schemeToolTip, registrableDomain, unicodeHost, asciiHost);

		return new(
			segments,
			schemeLabel,
			schemeState,
			schemeToolTip,
			BuildToolTip(toolTipLines),
			toolTipLines,
			registrableDomain
		);
	}

	private static string GetCanonicalFilePath(Uri uri)
	{
		var path = uri.LocalPath.Replace('/', '\\');
		if (path.Length >= 2 && path[1] == ':')
		{
			path = char.ToUpperInvariant(path[0]) + path[1..];
		}

		return path;
	}

	private static IReadOnlyList<UrlDisplaySegment> BuildFileSegments(string filePath)
	{
		if (filePath.Length >= 2 && filePath[1] == ':')
		{
			return BuildRootedFileSegments(filePath[..2], filePath[2..]);
		}

		if (filePath.StartsWith(@"\\", StringComparison.Ordinal))
		{
			var serverEnd = filePath.IndexOf('\\', 2);
			if (serverEnd > 2)
			{
				return BuildRootedFileSegments(filePath[..serverEnd], filePath[serverEnd..]);
			}
		}

		return BuildPathSegments(filePath);
	}

	private static IReadOnlyList<UrlDisplaySegment> BuildRootedFileSegments(string root, string remainingPath)
	{
		var segments = new List<UrlDisplaySegment> { new(root, UrlDisplaySegmentKind.FileRoot) };
		segments.AddRange(BuildPathSegments(remainingPath));
		return segments;
	}

	private static IReadOnlyList<UrlDisplaySegment> BuildPathSegments(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return [];
		}

		var fileNameStart = path.LastIndexOf('\\') + 1;
		if (fileNameStart <= 0 || fileNameStart >= path.Length)
		{
			return [new UrlDisplaySegment(path, UrlDisplaySegmentKind.Path)];
		}

		return
		[
			new UrlDisplaySegment(path[..fileNameStart], UrlDisplaySegmentKind.Path),
			new UrlDisplaySegment(path[fileNameStart..], UrlDisplaySegmentKind.FileName),
		];
	}

	private static IReadOnlyList<UrlToolTipLine> BuildFileToolTipLines(Uri uri, string filePath)
	{
		return
		[
			new UrlToolTipLine("Scheme", BuildSchemeToolTip(uri, UrlSecuritySchemeState.Neutral)),
			new UrlToolTipLine("Path", filePath),
			new UrlToolTipLine("Note", "No network request was made."),
		];
	}

	private static UrlSecuritySchemeState GetSchemeState(Uri uri)
	{
		return uri.Scheme switch
		{
			"https" => UrlSecuritySchemeState.Secure,
			"http" => UrlSecuritySchemeState.Insecure,
			_ => UrlSecuritySchemeState.Neutral,
		};
	}

	private static string BuildSchemeToolTip(Uri uri, UrlSecuritySchemeState schemeState)
	{
		return schemeState switch
		{
			UrlSecuritySchemeState.Secure => "HTTPS (secure scheme; TLS was not checked)",
			UrlSecuritySchemeState.Insecure => "HTTP (not encrypted)",
			_ => $"{uri.Scheme.ToUpperInvariant()} (local scheme)",
		};
	}

	private static IReadOnlyList<UrlToolTipLine> BuildToolTipLines(
		string schemeToolTip,
		string? registrableDomain,
		string? unicodeHost,
		string? asciiHost
	)
	{
		var lines = new List<UrlToolTipLine> { new("Scheme", schemeToolTip) };

		if (!string.IsNullOrWhiteSpace(unicodeHost))
		{
			lines.Add(new UrlToolTipLine("Host", unicodeHost));
		}

		if (!string.IsNullOrWhiteSpace(registrableDomain))
		{
			lines.Add(new UrlToolTipLine("Highlighted domain", registrableDomain));
		}

		if (
			!string.IsNullOrWhiteSpace(unicodeHost)
			&& !string.IsNullOrWhiteSpace(asciiHost)
			&& !string.Equals(unicodeHost, asciiHost, StringComparison.OrdinalIgnoreCase)
		)
		{
			lines.Add(new UrlToolTipLine("IDN ASCII", asciiHost));
		}

		lines.Add(new UrlToolTipLine("Note", "No network request was made."));
		return lines;
	}

	private static string BuildToolTip(IReadOnlyList<UrlToolTipLine> lines)
	{
		return string.Join(Environment.NewLine, lines.Select(line => $"{line.Label}: {line.Value}"));
	}

	private static IReadOnlyList<UrlDisplaySegment> BuildSegments(
		string displayUrl,
		Uri uri,
		out string? registrableDomain,
		out string? unicodeHost,
		out string? asciiHost
	)
	{
		registrableDomain = null;
		unicodeHost = null;
		asciiHost = null;

		if (!TryGetHostRange(displayUrl, out var hostStart, out var hostLength))
		{
			return BuildSchemeOnlySegments(displayUrl);
		}

		var segments = new List<UrlDisplaySegment>();
		AddIfNotEmpty(segments, displayUrl[..hostStart], UrlDisplaySegmentKind.Scheme);

		var hostText = displayUrl.Substring(hostStart, hostLength);
		asciiHost = GetAsciiHost(uri, hostText);
		unicodeHost = GetUnicodeHost(asciiHost);
		var hostDisplayText =
			!string.IsNullOrWhiteSpace(unicodeHost)
			&& !string.Equals(unicodeHost, asciiHost, StringComparison.OrdinalIgnoreCase)
				? unicodeHost
				: hostText;
		AddHostSegments(segments, hostDisplayText, asciiHost, out registrableDomain);

		AddTrailingSegments(segments, displayUrl[(hostStart + hostLength)..]);

		return segments;
	}

	private static IReadOnlyList<UrlDisplaySegment> BuildSchemeOnlySegments(string displayUrl)
	{
		var schemeEnd = displayUrl.IndexOf(':');
		if (schemeEnd < 0)
		{
			return [new UrlDisplaySegment(displayUrl, UrlDisplaySegmentKind.Other)];
		}

		var segments = new List<UrlDisplaySegment>();
		var pathStart = schemeEnd + 1;
		if (displayUrl[pathStart..].StartsWith("//", StringComparison.Ordinal))
		{
			pathStart += 2;
		}

		AddIfNotEmpty(segments, displayUrl[..pathStart], UrlDisplaySegmentKind.Scheme);
		AddTrailingSegments(segments, displayUrl[pathStart..]);
		return segments;
	}

	private static bool TryGetHostRange(string displayUrl, out int hostStart, out int hostLength)
	{
		hostStart = 0;
		hostLength = 0;

		var schemeSeparator = displayUrl.IndexOf(':');
		if (schemeSeparator < 0 || schemeSeparator + 2 >= displayUrl.Length)
		{
			return false;
		}

		var authorityStart = schemeSeparator + 1;
		if (!displayUrl[authorityStart..].StartsWith("//", StringComparison.Ordinal))
		{
			return false;
		}

		authorityStart += 2;
		var authorityEnd = displayUrl.IndexOfAny(['/', '?', '#'], authorityStart);
		if (authorityEnd < 0)
		{
			authorityEnd = displayUrl.Length;
		}

		var authority = displayUrl[authorityStart..authorityEnd];
		var userInfoEnd = authority.LastIndexOf('@');
		hostStart = authorityStart + userInfoEnd + 1;
		if (hostStart >= authorityEnd)
		{
			return false;
		}

		if (displayUrl[hostStart] == '[')
		{
			var bracketEnd = displayUrl.IndexOf(']', hostStart + 1);
			if (bracketEnd < 0 || bracketEnd >= authorityEnd)
			{
				return false;
			}

			hostLength = bracketEnd - hostStart + 1;
			return true;
		}

		var hostEnd = displayUrl.IndexOf(':', hostStart, authorityEnd - hostStart);
		if (hostEnd < 0)
		{
			hostEnd = authorityEnd;
		}

		hostLength = hostEnd - hostStart;
		return hostLength > 0;
	}

	private static string? GetAsciiHost(Uri uri, string hostText)
	{
		var host = TrimHost(hostText);
		if (string.IsNullOrWhiteSpace(host))
		{
			return null;
		}

		if (IPAddress.TryParse(host, out _))
		{
			return host;
		}

		try
		{
			return Idn.GetAscii(host).TrimEnd('.').ToLowerInvariant();
		}
		catch
		{
			return string.IsNullOrWhiteSpace(uri.IdnHost) ? host : uri.IdnHost.TrimEnd('.').ToLowerInvariant();
		}
	}

	private static string? GetUnicodeHost(string? asciiHost)
	{
		if (string.IsNullOrWhiteSpace(asciiHost) || IPAddress.TryParse(asciiHost, out _))
		{
			return asciiHost;
		}

		try
		{
			return Idn.GetUnicode(asciiHost).TrimEnd('.');
		}
		catch
		{
			return asciiHost;
		}
	}

	private static void AddHostSegments(
		List<UrlDisplaySegment> segments,
		string hostText,
		string? asciiHost,
		out string? registrableDomain
	)
	{
		registrableDomain = null;
		var displayHost = TrimHost(hostText);
		if (string.IsNullOrWhiteSpace(displayHost) || string.IsNullOrWhiteSpace(asciiHost))
		{
			AddIfNotEmpty(segments, hostText, UrlDisplaySegmentKind.Host);
			return;
		}

		var labels = displayHost.TrimEnd('.').Split('.');
		var registrableLabelCount = GetRegistrableLabelCount(asciiHost);
		if (registrableLabelCount is null || labels.Length < registrableLabelCount)
		{
			AddIfNotEmpty(
				segments,
				hostText,
				ContainsNonAscii(hostText) ? UrlDisplaySegmentKind.NonAsciiHost : UrlDisplaySegmentKind.Host
			);
			return;
		}

		var registrableStart = labels.Length - registrableLabelCount.Value;
		registrableDomain = GetRegistrableDomain(asciiHost, registrableLabelCount.Value);

		for (var i = 0; i < labels.Length; i++)
		{
			if (i > 0)
			{
				segments.Add(
					new UrlDisplaySegment(
						".",
						i >= registrableStart ? UrlDisplaySegmentKind.RegistrableDomain : UrlDisplaySegmentKind.Host
					)
				);
			}

			var kind = i >= registrableStart ? UrlDisplaySegmentKind.RegistrableDomain : UrlDisplaySegmentKind.Host;
			AddHostLabelSegments(segments, labels[i], kind);
		}

		if (hostText.EndsWith(".", StringComparison.Ordinal))
		{
			segments.Add(new UrlDisplaySegment(".", UrlDisplaySegmentKind.Host));
		}
	}

	private static int? GetRegistrableLabelCount(string asciiHost)
	{
		var normalized = asciiHost.TrimEnd('.').ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(normalized) || IPAddress.TryParse(normalized, out _))
		{
			return null;
		}

		var labels = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
		if (labels.Length < 2)
		{
			return null;
		}

		foreach (var suffix in KnownMultiPartSuffixes)
		{
			if (
				normalized.EndsWith($".{suffix.Suffix}", StringComparison.OrdinalIgnoreCase)
				&& labels.Length > suffix.LabelCount
			)
			{
				return suffix.LabelCount + 1;
			}
		}

		return labels[^1].All(c => c is >= 'a' and <= 'z') ? 2 : null;
	}

	private static string GetRegistrableDomain(string asciiHost, int registrableLabelCount)
	{
		var labels = asciiHost.TrimEnd('.').Split('.', StringSplitOptions.RemoveEmptyEntries);
		return string.Join('.', labels.Skip(labels.Length - registrableLabelCount));
	}

	private static string TrimHost(string hostText)
	{
		var host = hostText;
		if (host.StartsWith("[", StringComparison.Ordinal) && host.EndsWith("]", StringComparison.Ordinal))
		{
			host = host[1..^1];
		}

		return host.TrimEnd('.');
	}

	private static void AddTrailingSegments(List<UrlDisplaySegment> segments, string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}

		var queryStart = text.IndexOf('?');
		var fragmentStart = text.IndexOf('#');
		var pathEnd = text.Length;
		if (queryStart >= 0)
		{
			pathEnd = queryStart;
		}
		else if (fragmentStart >= 0)
		{
			pathEnd = fragmentStart;
		}

		AddIfNotEmpty(segments, text[..pathEnd], UrlDisplaySegmentKind.Path);

		if (queryStart >= 0)
		{
			var queryEnd = fragmentStart >= 0 && fragmentStart > queryStart ? fragmentStart : text.Length;
			AddIfNotEmpty(segments, text[queryStart..queryEnd], UrlDisplaySegmentKind.Query);
		}

		if (fragmentStart >= 0)
		{
			AddIfNotEmpty(segments, text[fragmentStart..], UrlDisplaySegmentKind.Fragment);
		}
	}

	private static bool ContainsNonAscii(string text)
	{
		return text.Any(c => c > 0x7f);
	}

	private static void AddHostLabelSegments(List<UrlDisplaySegment> segments, string label, UrlDisplaySegmentKind kind)
	{
		if (string.IsNullOrEmpty(label))
		{
			return;
		}

		var segmentStart = 0;
		for (var i = 0; i < label.Length; i++)
		{
			if (label[i] <= 0x7f)
			{
				continue;
			}

			AddIfNotEmpty(segments, label[segmentStart..i], kind);
			segments.Add(new UrlDisplaySegment(label[i].ToString(), UrlDisplaySegmentKind.NonAsciiHost));
			segmentStart = i + 1;
		}

		AddIfNotEmpty(segments, label[segmentStart..], kind);
	}

	private static void AddIfNotEmpty(List<UrlDisplaySegment> segments, string text, UrlDisplaySegmentKind kind)
	{
		if (!string.IsNullOrEmpty(text))
		{
			segments.Add(new UrlDisplaySegment(text, kind));
		}
	}
}
