using System;
using System.Diagnostics;
using BrowserPicker.Common.Resources;
using Microsoft.Win32;

namespace BrowserPicker.Windows;

/// <summary>
/// Which app Windows opens http and https links with, as stored in the user's default app choice.
/// </summary>
/// <param name="HttpProgId">The ProgId Windows uses for http links, or null when no choice is stored.</param>
/// <param name="HttpsProgId">The ProgId Windows uses for https links, or null when no choice is stored.</param>
public sealed record DefaultBrowserStatus(string? HttpProgId, string? HttpsProgId)
{
	/// <summary>ProgId of the installed (MSI) Browser Picker.</summary>
	public const string MachineProgId = "BrowserPicker";

	/// <summary>ProgId of the portable Browser Picker registered for the current user.</summary>
	public const string PortableProgId = "BrowserPickerPortable";

	private const string UrlAssociationsKey = @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations";

	/// <summary>True when Windows opens both http and https links with Browser Picker.</summary>
	public bool IsDefault => IsBrowserPicker(HttpProgId) && IsBrowserPicker(HttpsProgId);

	/// <summary>
	/// A sentence for the settings window describing whether Browser Picker opens web links.
	/// </summary>
	public string Description
	{
		get
		{
			var http = IsBrowserPicker(HttpProgId);
			var https = IsBrowserPicker(HttpsProgId);
			if (http && https)
			{
				return Strings.DefaultStatus_OpensWebLinks;
			}

			if (http || https)
			{
				var (other, otherProgId) = http ? ("https", HttpsProgId) : ("http", HttpProgId);
				return string.Format(
					Strings.DefaultStatus_PartialScheme,
					http ? "http" : "https",
					other,
					Name(otherProgId)
				);
			}

			if (HttpsProgId == null && HttpProgId == null)
			{
				return Strings.DefaultStatus_NoDefault;
			}

			return string.Format(Strings.DefaultStatus_OtherApp, Name(HttpsProgId ?? HttpProgId));

			static string Name(string? progId) =>
				progId == null ? Strings.DefaultStatus_NoApp : string.Format(Strings.DefaultStatus_AnotherApp, progId);
		}
	}

	/// <summary>
	/// True when <paramref name="progId"/> is one of Browser Picker's ProgIds.
	/// </summary>
	public static bool IsBrowserPicker(string? progId) =>
		string.Equals(progId, MachineProgId, StringComparison.OrdinalIgnoreCase)
		|| string.Equals(progId, PortableProgId, StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Reads the current user's choice for http and https links from the registry.
	/// </summary>
	public static DefaultBrowserStatus Read() => new(ReadProgId("http"), ReadProgId("https"));

	/// <summary>
	/// Reads the ProgId Windows uses for <paramref name="scheme"/>. Recent Windows 11 builds keep the choice in
	/// <c>UserChoiceLatest\ProgId</c> and may stop updating <c>UserChoice</c>, so the newer key wins when present.
	/// </summary>
	public static string? ReadProgId(string scheme) =>
		ReadUserValue($@"{UrlAssociationsKey}\{scheme}\UserChoiceLatest\ProgId", "ProgId")
		?? ReadUserValue($@"{UrlAssociationsKey}\{scheme}\UserChoice", "ProgId");

	/// <summary>
	/// Builds the link that opens Windows Default apps, on Browser Picker's own page when it is registered.
	/// </summary>
	/// <param name="registeredAppUser">Name under <c>HKCU\Software\RegisteredApplications</c>, or null.</param>
	/// <param name="registeredAppMachine">Name under <c>HKLM\Software\RegisteredApplications</c>, or null.</param>
	/// <remarks>
	/// The parameters are supported on Windows 11 with the 2023-04 update or later; older versions ignore them
	/// and show the Default apps list.
	/// </remarks>
	public static string GetDefaultAppsUri(string? registeredAppUser, string? registeredAppMachine)
	{
		if (!string.IsNullOrEmpty(registeredAppUser))
		{
			return $"ms-settings:defaultapps?registeredAppUser={Uri.EscapeDataString(registeredAppUser)}";
		}

		if (!string.IsNullOrEmpty(registeredAppMachine))
		{
			return $"ms-settings:defaultapps?registeredAppMachine={Uri.EscapeDataString(registeredAppMachine)}";
		}

		return "ms-settings:defaultapps";
	}

	/// <summary>
	/// Opens Windows Default apps on the page of the Browser Picker registration this executable uses.
	/// Windows does not let apps make themselves the default browser, so the user confirms it there.
	/// </summary>
	public static void OpenDefaultApps()
	{
		Process.Start(
			new ProcessStartInfo(GetDefaultAppsUri(FindUserRegistration(), FindMachineRegistration()))
			{
				UseShellExecute = true,
			}
		);
	}

	private static string? FindUserRegistration()
	{
#if BROWSERPICKER_PORTABLE
		return
			UserDefaultBrowserRegistration.IsRegisteredForCurrentUser()
			&& !UserDefaultBrowserRegistration.IsCurrentExecutableMachineRegistered()
			? PortableProgId
			: null;
#else
		return null;
#endif
	}

	private static string? FindMachineRegistration()
	{
		try
		{
			using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\RegisteredApplications", false);
			return key?.GetValue(MachineProgId) != null ? MachineProgId : null;
		}
		catch
		{
			return null;
		}
	}

	private static string? ReadUserValue(string subKey, string valueName)
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(subKey, false);
			return key?.GetValue(valueName) as string is { Length: > 0 } value ? value : null;
		}
		catch
		{
			return null;
		}
	}
}
