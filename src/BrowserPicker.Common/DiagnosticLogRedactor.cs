using System;
using System.Linq;
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

	[GeneratedRegex(
		@"(?:[a-zA-Z][a-zA-Z0-9+.\-]*://|\b(?:mailto|tel|sms|data|javascript|ext\+container):)[^\s""'<>]+",
		RegexOptions.IgnoreCase,
		100
	)]
	private static partial Regex UrlToken();

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

	private static string RedactToken(string token)
	{
		var trimmedLength = token.Length;
		while (trimmedLength > 0 && TrailingPunctuation.Contains(token[trimmedLength - 1]))
		{
			trimmedLength--;
		}
		return RedactUrl(token[..trimmedLength]) + token[trimmedLength..];
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
