namespace BrowserPicker.Common;

/// <summary>
/// Builds the command line used to open a URL in a browser.
/// </summary>
public static class LaunchArguments
{
	/// <summary>
	/// Joins the browser's arguments, the profile arguments, the privacy arguments and the quoted URL, in that order.
	/// </summary>
	/// <param name="commandArgs">Arguments configured for the browser.</param>
	/// <param name="profileArgs">Arguments that select a profile.</param>
	/// <param name="privacyArgs">Arguments for private mode, or null when not opening in private mode.</param>
	/// <param name="url">The URL to open.</param>
	/// <returns>The argument string to pass to the browser executable.</returns>
	public static string Build(string? commandArgs, string? profileArgs, string? privacyArgs, string url)
	{
		return Join(commandArgs, Join(profileArgs, PrefixUrl(privacyArgs, $"\"{url}\"")));
	}

	/// <summary>
	/// Puts the privacy arguments right before the URL. The built-in ones end with a space, but a user may type
	/// "--incognito" without one, which the browser would otherwise receive as "--incognitohttps://…".
	/// Arguments ending with "=" stay attached on purpose, for switches such as "--app=".
	/// </summary>
	private static string PrefixUrl(string? privacyArgs, string quotedUrl)
	{
		if (string.IsNullOrEmpty(privacyArgs))
		{
			return quotedUrl;
		}

		var last = privacyArgs[^1];
		return char.IsWhiteSpace(last) || last == '=' ? privacyArgs + quotedUrl : privacyArgs + " " + quotedUrl;
	}

	private static string Join(string? first, string second)
	{
		return string.IsNullOrEmpty(first) ? second : first + " " + second;
	}
}
