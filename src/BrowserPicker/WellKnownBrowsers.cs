using System;
using System.Collections.Generic;
using System.Linq;

namespace BrowserPicker;

// Note: I am not entirely happy with the design of this part, but it was the best I can do in a jiffy
public static class WellKnownBrowsers
{
	public static IWellKnownBrowser? Lookup(string? name, string? executable)
	{
		return List.FirstOrDefault(b => b.Name == name)
			?? List.FirstOrDefault(b => executable != null
				&& executable.Contains(b.Executable, StringComparison.CurrentCultureIgnoreCase)
			);
	}

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

public interface IWellKnownBrowser
{
	string Name { get; }
	string Executable { get; }
	string? RealExecutable { get; }
	string? PrivacyArgs { get; }
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