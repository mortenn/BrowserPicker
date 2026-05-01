namespace BrowserPicker.Common;

public sealed record SecurityOptions
{
	public bool ProbeRedirects { get; set; }
	public bool RedirectsKnownOnly { get; set; }
	public bool ProbeFavicons { get; set; }
	public bool FaviconsForDefaults { get; set; }
	public bool CheckCertificateRecords { get; set; }
	public bool HideManualConnectionCheck { get; set; }
	public bool SkipConnectionCheckConfirmation { get; set; }

	public static SecurityOptions Default =>
		new()
		{
			ProbeRedirects = true,
			RedirectsKnownOnly = true,
			ProbeFavicons = true,
			FaviconsForDefaults = true,
			CheckCertificateRecords = true,
			HideManualConnectionCheck = false,
			SkipConnectionCheckConfirmation = false,
		};

	public static SecurityOptions MaxPrivacy =>
		new()
		{
			ProbeRedirects = false,
			RedirectsKnownOnly = true,
			ProbeFavicons = false,
			FaviconsForDefaults = true,
			CheckCertificateRecords = false,
			HideManualConnectionCheck = true,
			SkipConnectionCheckConfirmation = false,
		};

	public static SecurityOptions EnableAll =>
		new()
		{
			ProbeRedirects = true,
			RedirectsKnownOnly = false,
			ProbeFavicons = true,
			FaviconsForDefaults = false,
			CheckCertificateRecords = true,
			HideManualConnectionCheck = false,
			SkipConnectionCheckConfirmation = true,
		};
}

public static class SecuritySettingsExtensions
{
	public static SecurityOptions GetSecurityOptions(this ISecuritySettings settings)
	{
		return new SecurityOptions
		{
			ProbeRedirects = settings.ProbeRedirects,
			RedirectsKnownOnly = settings.RedirectsKnownOnly,
			ProbeFavicons = settings.ProbeFavicons,
			FaviconsForDefaults = settings.FaviconsForDefaults,
			CheckCertificateRecords = settings.CheckCertificateRecords,
			HideManualConnectionCheck = settings.HideManualConnectionCheck,
			SkipConnectionCheckConfirmation = settings.SkipConnectionCheckConfirmation,
		};
	}

	public static void ApplySecurityOptions(this ISecuritySettings settings, SecurityOptions options)
	{
		settings.ProbeRedirects = options.ProbeRedirects;
		settings.RedirectsKnownOnly = options.RedirectsKnownOnly;
		settings.ProbeFavicons = options.ProbeFavicons;
		settings.FaviconsForDefaults = options.FaviconsForDefaults;
		settings.CheckCertificateRecords = options.CheckCertificateRecords;
		settings.HideManualConnectionCheck = options.HideManualConnectionCheck;
		settings.SkipConnectionCheckConfirmation = options.SkipConnectionCheckConfirmation;
	}
}
