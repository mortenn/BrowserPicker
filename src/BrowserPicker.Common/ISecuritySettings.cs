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

	/// <summary>
	/// When true, explicit certificate checks also inspect DNS CAA records and certificate transparency evidence.
	/// </summary>
	bool CheckCertificateRecords { get; set; }

	/// <summary>
	/// When true, hides the manual connection check action from the picker.
	/// </summary>
	bool HideManualConnectionCheck { get; set; }

	/// <summary>
	/// When true, manual connection checks start immediately without showing the confirmation prompt.
	/// </summary>
	bool SkipConnectionCheckConfirmation { get; set; }
}
