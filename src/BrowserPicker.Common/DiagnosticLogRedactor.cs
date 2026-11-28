using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BrowserPicker.Common;

/// <summary>
/// Removes private data from diagnostic log text before it is written to disk.
/// URLs are reduced to <c>scheme://host[:port]</c>, opaque URLs (mailto, data, ...) lose their content,
/// Firefox container URLs keep the container name but have their wrapped URL reduced,
/// and the current user's profile folder is replaced by <c>%USERPROFILE%</c>.
/// </summary>
public static partial class DiagnosticLogRedactor
{
	/// <summary>Marker that replaces removed URL content.</summary>
	public const string RedactedMarker = "[redacted]";

	private const string TrailingPunctuation = ".,;:!?)]}";
	private const string ContainerScheme = "ext+container:";

	// A URL or path right after a quote runs to the closing quote, so spaces inside it are covered.
	// Otherwise a URL runs to the next whitespace. Unquoted paths are left alone because they cannot be
	// delimited; the link being handled is redacted as a whole argument by FormatRedacted instead.
	[GeneratedRegex(
		@"(?<="")(?:[a-zA-Z][a-zA-Z0-9+.\-]*://|\b(?:mailto|tel|sms|data|javascript|ext\+container):|[a-zA-Z]:\\|\\\\)[^""]+"
			+ @"|(?:[a-zA-Z][a-zA-Z0-9+.\-]*://|\b(?:mailto|tel|sms|data|javascript|ext\+container):)[^\s""'<>]+",
		RegexOptions.IgnoreCase,
		100
	)]
	private static partial Regex UrlToken();

	[GeneratedRegex(
		@"^(?:[a-zA-Z][a-zA-Z0-9+.\-]*://|(?:mailto|tel|sms|data|javascript|ext\+container):)",
		RegexOptions.IgnoreCase
	)]
	private static partial Regex UrlStart();

	[GeneratedRegex(@"^(?:[a-zA-Z]:\\|\\\\|%USERPROFILE%\\)", RegexOptions.IgnoreCase)]
	private static partial Regex PathStart();

	/// <summary>
	/// Redacts <paramref name="text"/> using the current user's profile folder.
	/// </summary>
	/// <param name="text">The log text to redact.</param>
	/// <param name="includeFullUrls">When true, URLs are kept intact (only the profile folder is replaced).</param>
	public static string Redact(string text, bool includeFullUrls) =>
		Redact(text, includeFullUrls, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

	/// <summary>
	/// Redacts <paramref name="text"/>.
	/// </summary>
	/// <param name="text">The log text to redact.</param>
	/// <param name="includeFullUrls">When true, URLs are kept intact (only the profile folder is replaced).</param>
	/// <param name="userProfilePath">Folder to replace with <c>%USERPROFILE%</c>; null or empty to skip.</param>
	public static string Redact(string text, bool includeFullUrls, string? userProfilePath)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}

		var result = text;
		if (!includeFullUrls)
		{
			try
			{
				result = UrlToken().Replace(result, match => RedactToken(match.Value));
			}
			catch (RegexMatchTimeoutException)
			{
				// Never write text we could not inspect.
				return RedactedMarker;
			}
		}

		if (!string.IsNullOrEmpty(userProfilePath))
		{
			result = result.Replace(userProfilePath, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
		}

		return result;
	}

	/// <summary>
	/// Reduces a single URL to what is safe to keep. Exposed for tests.
	/// </summary>
	public static string RedactUrl(string url)
	{
		if (url.StartsWith(ContainerScheme, StringComparison.OrdinalIgnoreCase))
		{
			return RedactContainerUrl(url[ContainerScheme.Length..]);
		}

		var separator = url.IndexOf("://", StringComparison.Ordinal);
		if (separator < 0)
		{
			// Opaque URL such as mailto:, tel: or data: - the whole content is private.
			var colon = url.IndexOf(':');
			return colon < 0 ? RedactedMarker : $"{url[..(colon + 1)]}{RedactedMarker}";
		}

		var scheme = url[..separator];
		if (
			scheme.Equals("file", StringComparison.OrdinalIgnoreCase)
			|| !Uri.TryCreate(url, UriKind.Absolute, out var uri)
			|| uri.IsFile
			|| string.IsNullOrEmpty(uri.Host)
		)
		{
			return $"{scheme}://{RedactedMarker}";
		}

		var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
		var hasMore = uri.PathAndQuery != "/" || !string.IsNullOrEmpty(uri.Fragment);
		return $"{uri.Scheme}://{uri.Host}{port}{(hasMore ? "/" + RedactedMarker : string.Empty)}";
	}

	/// <summary>
	/// Formats a structured log message with each argument that is a URL or a file path redacted as a whole,
	/// so values with spaces (a file name, or a URL that <see cref="Uri.ToString"/> unescaped) are fully covered.
	/// Returns null when <paramref name="state"/> is not a structured message.
	/// </summary>
	public static string? FormatRedacted(object? state)
	{
		if (state is not IReadOnlyList<KeyValuePair<string, object?>> values)
		{
			return null;
		}
		if (values.FirstOrDefault(v => v.Key == "{OriginalFormat}").Value is not string template)
		{
			return null;
		}

		var builder = new StringBuilder(template.Length);
		for (var i = 0; i < template.Length; i++)
		{
			var c = template[i];
			if (c is '{' or '}' && i + 1 < template.Length && template[i + 1] == c)
			{
				builder.Append(c);
				i++;
				continue;
			}
			var close = c == '{' ? template.IndexOf('}', i) : -1;
			if (close < 0)
			{
				builder.Append(c);
				continue;
			}
			var hole = template[(i + 1)..close];
			var nameEnd = hole.IndexOfAny([':', ',']);
			var name = nameEnd < 0 ? hole : hole[..nameEnd];
			builder.Append(RedactValue(values.FirstOrDefault(v => v.Key == name).Value));
			i = close;
		}
		return builder.ToString();
	}

	/// <summary>
	/// Redacts one log argument: a URL keeps its scheme and host, and a file path keeps its root and extension.
	/// Executable paths are kept, because browser paths help diagnose launches and are not private.
	/// </summary>
	public static string RedactValue(object? value)
	{
		switch (value)
		{
			case null:
				return "(null)";
			case Uri { IsAbsoluteUri: true } uri:
				return RedactUrl(uri.OriginalString);
			case Uri:
				return RedactedMarker;
		}
		var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
		var trimmed = text.Trim().Trim('"');
		if (UrlStart().IsMatch(trimmed))
		{
			return RedactUrl(trimmed);
		}
		return PathStart().IsMatch(trimmed) ? RedactPath(trimmed) : text;
	}

	private static string RedactPath(string path)
	{
		var extension = Path.GetExtension(path);
		if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
		{
			return path;
		}
		var root = path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\" : path[..(path.IndexOf('\\') + 1)];
		return root + RedactedMarker + extension;
	}

	private static string RedactToken(string token)
	{
		var trimmedLength = token.Length;
		while (trimmedLength > 0 && TrailingPunctuation.Contains(token[trimmedLength - 1]))
		{
			trimmedLength--;
		}
		var value = token[..trimmedLength];
		var redacted = PathStart().IsMatch(value) ? RedactPath(value) : RedactUrl(value);
		return redacted + token[trimmedLength..];
	}

	private static string RedactContainerUrl(string query)
	{
		var parts = query
			.Split('&')
			.Select(part =>
			{
				if (!part.StartsWith("url=", StringComparison.OrdinalIgnoreCase))
				{
					return part;
				}
				string wrapped;
				try
				{
					wrapped = Uri.UnescapeDataString(part[4..]);
				}
				catch (UriFormatException)
				{
					return "url=" + RedactedMarker;
				}
				return "url=" + RedactUrl(wrapped);
			});
		return ContainerScheme + string.Join('&', parts);
	}
}
