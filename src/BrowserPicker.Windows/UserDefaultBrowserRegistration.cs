using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace BrowserPicker.Windows;

public enum CurrentUserBrowserRegistrationResult
{
	Registered,
	AlreadyRegisteredForCurrentUser,
	CurrentExecutableMachineRegistered,
	ExecutablePathUnavailable,
	RegistrationFailed,
}

/// <summary>
/// Registers Browser Picker as a browser for the current Windows user.
/// </summary>
public static class UserDefaultBrowserRegistration
{
	private const string ApplicationName = "Browser Picker (Portable)";
	private const string ApplicationId = "BrowserPickerPortable";
	private const string ProgId = "BrowserPickerPortable";
	private const string ApplicationDescription = "Shows a prompt to let you use different browsers on the fly.";
	private const string ApplicationCapabilitiesKey = @"Software\BrowserPickerPortable\Capabilities";
	private const string RegisteredApplicationsKey = @"Software\RegisteredApplications";
	private const string MachineBrowserPickerCommandKey = @"SOFTWARE\BrowserPicker\Capabilities\shell\open\command";

	public static void RegisterForCurrentUser()
	{
		if (!TryRegisterForCurrentUser(out var error))
		{
			throw new InvalidOperationException(error);
		}
	}

	public static CurrentUserBrowserRegistrationResult RegisterForCurrentUserIfPortableRelease(out string? detail)
	{
		detail = null;
		if (!TryGetExecutablePath(out var executablePath, out var error))
		{
			detail = error;
			return CurrentUserBrowserRegistrationResult.ExecutablePathUnavailable;
		}

		if (IsRegisteredForCurrentUser())
		{
			detail = "Browser Picker (Portable) is already registered for the current user.";
			return CurrentUserBrowserRegistrationResult.AlreadyRegisteredForCurrentUser;
		}

		var machineExecutablePath = GetMachineRegisteredExecutablePath();
		if (machineExecutablePath != null && PathsEqual(executablePath, machineExecutablePath))
		{
			detail = $"Current executable matches machine registration: {machineExecutablePath}";
			return CurrentUserBrowserRegistrationResult.CurrentExecutableMachineRegistered;
		}

		if (TryRegisterForCurrentUser(out error))
		{
			detail = $"Registered Browser Picker (Portable) for {executablePath}.";
			return CurrentUserBrowserRegistrationResult.Registered;
		}

		detail = error;
		return CurrentUserBrowserRegistrationResult.RegistrationFailed;
	}

	public static bool IsCurrentExecutableMachineRegistered()
	{
		if (!TryGetExecutablePath(out var executablePath, out _))
		{
			return false;
		}

		var machineExecutablePath = GetMachineRegisteredExecutablePath();
		return machineExecutablePath != null && PathsEqual(executablePath, machineExecutablePath);
	}

	public static void UnregisterForCurrentUser()
	{
		using var registeredApplications = Registry.CurrentUser.OpenSubKey(RegisteredApplicationsKey, true);
		registeredApplications?.DeleteValue(ApplicationId, throwOnMissingValue: false);

		Registry.CurrentUser.DeleteSubKeyTree(@"Software\BrowserPickerPortable", throwOnMissingSubKey: false);
		Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + ProgId, throwOnMissingSubKey: false);
	}

	public static bool IsRegisteredForCurrentUser()
	{
		try
		{
			using var registeredApplications = Registry.CurrentUser.OpenSubKey(RegisteredApplicationsKey, false);
			return string.Equals(
				registeredApplications?.GetValue(ApplicationId) as string,
				ApplicationCapabilitiesKey,
				StringComparison.OrdinalIgnoreCase
			);
		}
		catch
		{
			return false;
		}
	}

	private static bool TryRegisterForCurrentUser([NotNullWhen(false)] out string? error)
	{
		error = null;
		try
		{
			if (!TryGetExecutablePath(out var executablePath, out error))
			{
				return false;
			}

			RemoveLegacyCurrentUserRegistration();

			RegisterProgId(executablePath);
			RegisterCapabilities(ApplicationCapabilitiesKey, executablePath);

			using var registeredApplications = Registry.CurrentUser.CreateSubKey(RegisteredApplicationsKey);
			if (registeredApplications == null)
			{
				error = @"Unable to create HKCU\Software\RegisteredApplications.";
				return false;
			}

			registeredApplications.SetValue(ApplicationId, ApplicationCapabilitiesKey, RegistryValueKind.String);
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
	}

	private static void RegisterProgId(string executablePath)
	{
		using var progId = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId);
		progId.SetValue(null, ApplicationName, RegistryValueKind.String);
		progId.SetValue("EditFlags", 2, RegistryValueKind.DWord);
		progId.SetValue("FriendlyTypeName", "Web URL", RegistryValueKind.String);
		progId.SetValue("URL Protocol", string.Empty, RegistryValueKind.String);

		using var icon = progId.CreateSubKey("DefaultIcon");
		icon.SetValue(null, $"{executablePath},0", RegistryValueKind.String);

		using var shell = progId.CreateSubKey("shell");
		shell.SetValue(null, "open", RegistryValueKind.String);

		using var command = progId.CreateSubKey(@"shell\open\command");
		command.SetValue(null, BuildCommand(executablePath), RegistryValueKind.String);
	}

	private static void RegisterCapabilities(string keyPath, string executablePath)
	{
		using var capabilities = Registry.CurrentUser.CreateSubKey(keyPath);
		capabilities.SetValue("ApplicationDescription", ApplicationDescription, RegistryValueKind.String);
		capabilities.SetValue("ApplicationIcon", $"{executablePath},0", RegistryValueKind.String);
		capabilities.SetValue("ApplicationName", ApplicationName, RegistryValueKind.String);

		using var defaultIcon = capabilities.CreateSubKey("DefaultIcon");
		defaultIcon.SetValue(null, $"{executablePath},0", RegistryValueKind.String);

		using var fileAssociations = capabilities.CreateSubKey("FileAssociations");
		fileAssociations.SetValue(".html", ProgId, RegistryValueKind.String);
		fileAssociations.SetValue(".htm", ProgId, RegistryValueKind.String);

		using var startMenu = capabilities.CreateSubKey("StartMenu");
		startMenu.SetValue("StartMenuInternet", ApplicationId, RegistryValueKind.String);

		using var urlAssociations = capabilities.CreateSubKey("URLAssociations");
		urlAssociations.SetValue("ftp", ProgId, RegistryValueKind.String);
		urlAssociations.SetValue("http", ProgId, RegistryValueKind.String);
		urlAssociations.SetValue("https", ProgId, RegistryValueKind.String);

		using var shell = capabilities.CreateSubKey("shell");
		shell.SetValue(null, "open", RegistryValueKind.String);

		using var command = capabilities.CreateSubKey(@"shell\open\command");
		command.SetValue(null, BuildCommand(executablePath), RegistryValueKind.String);
	}

	private static string BuildCommand(string executablePath) => $"\"{executablePath}\" \"%1\"";

	private static string? GetMachineRegisteredExecutablePath()
	{
		try
		{
			using var command = Registry.LocalMachine.OpenSubKey(MachineBrowserPickerCommandKey, false);
			return TryGetExecutablePathFromCommand(command?.GetValue(null) as string, out var executablePath)
				? executablePath
				: null;
		}
		catch
		{
			return null;
		}
	}

	private static bool TryGetExecutablePathFromCommand(string? command, [NotNullWhen(true)] out string? executablePath)
	{
		executablePath = null;
		if (string.IsNullOrWhiteSpace(command))
		{
			return false;
		}

		var trimmed = command.Trim();
		if (trimmed.StartsWith('"'))
		{
			var endQuote = trimmed.IndexOf('"', 1);
			if (endQuote > 1)
			{
				executablePath = trimmed[1..endQuote];
				return true;
			}
		}

		var exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
		if (exeIndex < 0)
		{
			return false;
		}

		executablePath = trimmed[..(exeIndex + 4)];
		return true;
	}

	private static bool PathsEqual(string first, string second)
	{
		try
		{
			return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
		}
	}

	private static void RemoveLegacyCurrentUserRegistration()
	{
		using var registeredApplications = Registry.CurrentUser.OpenSubKey(RegisteredApplicationsKey, true);
		registeredApplications?.DeleteValue("BrowserPicker", throwOnMissingValue: false);

		Registry.CurrentUser.DeleteSubKeyTree(@"Software\BrowserPicker\Capabilities", throwOnMissingSubKey: false);
		Registry.CurrentUser.DeleteSubKeyTree(
			@"Software\Clients\StartMenuInternet\BrowserPicker",
			throwOnMissingSubKey: false
		);
		Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\BrowserPicker", throwOnMissingSubKey: false);
	}

	private static bool TryGetExecutablePath(
		[NotNullWhen(true)] out string? executablePath,
		[NotNullWhen(false)] out string? error
	)
	{
		executablePath =
			Environment.ProcessPath
			?? Assembly.GetEntryAssembly()?.Location
			?? Process.GetCurrentProcess().MainModule?.FileName;

		if (string.IsNullOrWhiteSpace(executablePath))
		{
			error = "Unable to determine the Browser Picker executable path.";
			return false;
		}

		if (!File.Exists(executablePath))
		{
			error = $"The Browser Picker executable was not found: {executablePath}";
			return false;
		}

		error = null;
		return true;
	}
}
