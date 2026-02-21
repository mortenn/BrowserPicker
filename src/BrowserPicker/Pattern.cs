using System.Text.RegularExpressions;


#if DEBUG
#endif

namespace BrowserPicker;

/// <summary>
/// Compiled regex patterns used for HTML and URL parsing (e.g. favicon discovery).
/// </summary>
internal static partial class Pattern
{
	/// <summary>
	/// Matches HTML link elements with rel="icon" (or similar) for favicon discovery.
	/// </summary>
	[GeneratedRegex("<link[^>]*rel=.?icon[^>]/>", RegexOptions.IgnoreCase, 100)]
	public static partial Regex HtmlLink();

	/// <summary>
	/// Matches href attribute values containing http/https URLs (e.g. inside a link tag).
	/// </summary>
	[GeneratedRegex("href=[\"']?(https?://\\S*)[\"']?", RegexOptions.IgnoreCase, 100)]
	public static partial Regex LinkHref();
}