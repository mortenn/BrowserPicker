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

	/// <summary>
	/// When true, manual connection checks also look up the registrable domain's registration data over RDAP.
	/// Sends the registrable domain (not the URL) to the registry's RDAP service.
	/// </summary>
	bool LookupDomainRegistration { get; set; }

	/// <summary>
	/// When true, manual connection checks also look up the host's public certificate transparency history.
	/// Sends the host name (not the URL) to crt.sh.
	/// </summary>
	bool LookupCertificateHistory { get; set; }

	/// <summary>
	/// When true, manual connection checks also look up the host on URLhaus (abuse.ch), using the user's own
	/// Auth-Key. The key is kept in Windows Credential Manager, not in the settings file.
	/// </summary>
	bool LookupUrlReputation { get; set; }
}
