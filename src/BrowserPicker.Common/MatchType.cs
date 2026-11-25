namespace BrowserPicker.Common;

/// <summary>
/// How a default rule matches a URL.
/// </summary>
public enum MatchType
{
	/// <summary>URL hostname ends with the pattern.</summary>
	Hostname,

	/// <summary>URL string starts with the pattern.</summary>
	Prefix,

	/// <summary>URL is matched by the pattern as a regular expression.</summary>
	Regex,

	/// <summary>Fallback default when no other rule matches.</summary>
	Default,

	/// <summary>URL string contains the pattern.</summary>
	Contains,

	/// <summary>URL is a local or network file inside the folder in the pattern, or one of its subfolders.</summary>
	Folder,
}
