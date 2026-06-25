using System;

namespace BrowserPicker.Common;

/// <summary>
/// Helpers for repairing the process environment WPF relies on during startup.
/// </summary>
public static class WindowsEnvironment
{
	/// <summary>The environment variable WPF's font cache uses to locate the Windows Fonts directory.</summary>
	public const string WindowsDirectoryVariable = "windir";

	/// <summary>
	/// Ensures the process-level <c>windir</c> environment variable points at a valid Windows directory.
	/// WPF's font subsystem (<c>MS.Internal.FontCache.Util</c>) builds the Windows Fonts path from this
	/// variable and throws <see cref="UriFormatException"/> during startup when it is missing or empty.
	/// Some hosts (e.g. the Codex desktop app launching the Azure DevOps MCP auth flow) start
	/// BrowserPicker with a stripped environment where <c>windir</c> is absent. See issue #299.
	/// </summary>
	/// <returns>The resolved Windows directory, or <see langword="null"/> when none could be determined.</returns>
	public static string? EnsureWindowsDirectory()
	{
		var windir = Environment.GetEnvironmentVariable(WindowsDirectoryVariable);
		if (!string.IsNullOrWhiteSpace(windir))
		{
			return windir;
		}

		// SystemRoot carries the same value and is less likely to be stripped; fall back to the
		// OS-resolved Windows folder (which does not depend on environment variables) as a last resort.
		var fallback = Environment.GetEnvironmentVariable("SystemRoot");
		if (string.IsNullOrWhiteSpace(fallback))
		{
			fallback = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
		}

		if (string.IsNullOrWhiteSpace(fallback))
		{
			return null;
		}

		Environment.SetEnvironmentVariable(WindowsDirectoryVariable, fallback, EnvironmentVariableTarget.Process);
		return fallback;
	}
}
