namespace BrowserPicker.Common;

/// <summary>
/// Security and privacy related settings that control automatic URL probing behavior.
/// </summary>
public interface ISecuritySettings
{
	/// <summary>
	/// When true, probes URLs for redirect targets.
	/// </summary>
	bool ProbeRedirects { get; set; }

	/// <summary>
	/// When true, only probes redirects for configured shortener hosts.
	/// </summary>
	bool RedirectsKnownOnly { get; set; }

	/// <summary>
	/// When true, probes URLs for a favicon to show in the picker UI.
	/// </summary>
	bool ProbeFavicons { get; set; }

	/// <summary>
	/// When true, only probes favicons for URLs matching a Defaults rule.
	/// </summary>
	bool FaviconsForDefaults { get; set; }
}
