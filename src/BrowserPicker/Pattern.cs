using System.Text.RegularExpressions;


#if DEBUG
#endif

namespace BrowserPicker;

internal static partial class Pattern
{
	[GeneratedRegex("<link[^>]*rel=.?icon[^>]/>", RegexOptions.IgnoreCase, 100)]
	public static partial Regex HtmlLink();

	[GeneratedRegex("href=[\"']?(https?://\\S*)[\"']?", RegexOptions.IgnoreCase, 100)]
	public static partial Regex LinkHref();
}