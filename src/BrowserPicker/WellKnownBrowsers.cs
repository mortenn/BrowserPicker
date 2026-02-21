using System;
using System.Collections.Generic;
using System.Linq;

namespace BrowserPicker;

/// <summary>
/// Registry of well-known browsers with default names, executables, and privacy arguments.
/// </summary>
public static class WellKnownBrowsers
{
	/// <summary>
	/// Finds a well-known browser by display name or by executable path.
	/// </summary>
	/// <param name="name">Browser display name to match.</param>
	/// <param name="executable">Executable path to match (optional).</param>
	/// <returns>Matching <see cref="IWellKnownBrowser"/>, or null.</returns>
	public static IWellKnownBrowser? Lookup(string? name, string? executable)
	{
		return List.FirstOrDefault(b => b.Name == name)
			?? List.FirstOrDefault(b => executable != null
				&& executable.Contains(b.Executable, StringComparison.CurrentCultureIgnoreCase)
			);
	}

	/// <summary>
	/// All well-known browser definitions in display order.
	/// </summary>
	public static readonly List<IWellKnownBrowser> List =
	[
		FirefoxDevEdition.Instance,
		Firefox.Instance,
		ChromeDevEdition.Instance,
		Chrome.Instance,
		MicrosoftEdge.Instance,
		Edge.Instance,
		InternetExplorer.Instance,
		OperaStable.Instance
	];
}

/// <summary>
/// Defines a well-known browser: display name, executable, and privacy mode arguments.
/// </summary>
public interface IWellKnownBrowser
{
	/// <summary>Display name of the browser.</summary>
	string Name { get; }
	/// <summary>Primary executable or protocol (e.g. "chrome.exe", "microsoft-edge:").</summary>
	string Executable { get; }
	/// <summary>Actual executable path when different from <see cref="Executable"/> (e.g. for protocol handlers).</summary>
	string? RealExecutable { get; }
	/// <summary>Arguments to pass for private/privacy mode.</summary>
	string? PrivacyArgs { get; }
	/// <summary>Label for the privacy mode action (e.g. "Open incognito").</summary>
	string PrivacyMode { get; }
}

public sealed class Firefox : IWellKnownBrowser
{
	public static readonly Firefox Instance = new();

	public string Name => "Mozilla Firefox";

	public string Executable => "firefox.exe";

	public string? RealExecutable => null;

	public string PrivacyArgs => "-private-window ";

	public string PrivacyMode => "Open with private browsing";
}

public sealed class FirefoxDevEdition : IWellKnownBrowser
{
	public static readonly FirefoxDevEdition Instance = new();

	public string Name => "Firefox Developer Edition";

	public string Executable => "firefox.exe";

	public string? RealExecutable => null;

	public string PrivacyArgs => "-private-window ";

	public string PrivacyMode => "Open with private browsing";
}

public sealed class Chrome : IWellKnownBrowser
{
	public static readonly Chrome Instance = new();

	public string Name => "Google Chrome";

	public string Executable => "chrome.exe";

	public string? RealExecutable => null;

	public string PrivacyArgs => "--incognito ";

	public string PrivacyMode => "Open incognito";
}

public sealed class ChromeDevEdition : IWellKnownBrowser
{
	public static readonly ChromeDevEdition Instance = new();

	public string Name => "Google Chrome Dev";

	public string Executable => "chrome.exe";

	public string? RealExecutable => null;

	public string PrivacyArgs => "--incognito ";

	public string PrivacyMode => "Open incognito";
}

public sealed class MicrosoftEdge : IWellKnownBrowser
{
	public static readonly MicrosoftEdge Instance = new();

	public string Name => "Microsoft Edge";

	public string Executable => "msedge.exe";

	public string? RealExecutable => null;

	public string PrivacyArgs => "-inprivate ";

	public string PrivacyMode => "Open in private mode";
}

public sealed class Edge : IWellKnownBrowser
{
	public static readonly Edge Instance = new();

	public string Name => "Edge";

	public string Executable => "microsoft-edge:";

	public string? RealExecutable => null;

	public string PrivacyArgs => "-private ";

	public string PrivacyMode => "Open in private mode";
}

public sealed class InternetExplorer : IWellKnownBrowser
{
	public static readonly InternetExplorer Instance = new();

	public string Name => "Internet Explorer";

	public string Executable => "iexplore.exe";

	public string? RealExecutable => null;

	public string PrivacyArgs => "-private ";

	public string PrivacyMode => "Open in private mode";
}

public sealed class OperaStable : IWellKnownBrowser
{
	public static readonly OperaStable Instance = new();

	public string Name => "Opera Stable";

	public string Executable => "Opera\\Launcher.exe";

	public string RealExecutable => "opera.exe";

	public string PrivacyArgs => "--private ";

	public string PrivacyMode => "Open in private mode";
}