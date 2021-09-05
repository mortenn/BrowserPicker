using System;
using System.Collections.Generic;
using System.Linq;

namespace BrowserPicker
{
	// Note: I am not entirely happy with the design of this part, but it was the best I can do in a jiffy
	public static class WellKnownBrowsers
	{
		public static IWellKnownBrowser Lookup(string name, string executable)
		{
			return List.FirstOrDefault(b => b.Name == name || executable != null && executable.IndexOf(b.Executable, StringComparison.CurrentCultureIgnoreCase) != -1);
		}

		public readonly static List<IWellKnownBrowser> List = new List<IWellKnownBrowser>
		{ 
			new Firefox(),
			new Chrome(),
			new MicrosoftEdge(),
			new Edge(),
			new InternetExplorer(),
			new OperaStable()
		};
	}

	public interface IWellKnownBrowser
	{
		string Name { get; }
		string Executable { get; }
		string RealExecutable { get; }
		string PrivacyArgs { get; }
		string PrivacyMode { get; }
	}

	public class Firefox : IWellKnownBrowser
	{
		public string Name => "Mozilla Firefox";

		public string Executable => "firefox.exe";

		public string RealExecutable => null;

		public string PrivacyArgs => "-private-window ";

		public string PrivacyMode => "Open with private browsing";
	}

	public class Chrome : IWellKnownBrowser
	{
		public string Name => "Google Chrome";

		public string Executable => "chrome.exe";

		public string RealExecutable => null;

		public string PrivacyArgs => "--incognito ";

		public string PrivacyMode => "Open incognito";
	}

	public class MicrosoftEdge : IWellKnownBrowser
	{
		public string Name => "Microsoft Edge";

		public string Executable => "msedge.exe";

		public string RealExecutable => null;
	
		public string PrivacyArgs => "-inprivate ";

		public string PrivacyMode => "Open in private mode";
	}

	public class Edge : IWellKnownBrowser
	{
		public string Name => "Edge";

		public string Executable => "microsoft-edge:";

		public string RealExecutable => null;
	
		public string PrivacyArgs => "-private ";

		public string PrivacyMode => "Open in private mode";
	}

	public class InternetExplorer : IWellKnownBrowser
	{
		public string Name => "Internet Explorer";

		public string Executable => "iexplore.exe";

		public string RealExecutable => null;
	
		public string PrivacyArgs => "-private ";

		public string PrivacyMode => "Open in private mode";
	}

	public class OperaStable : IWellKnownBrowser
	{
		public string Name => "Opera Stable";

		public string Executable => "Opera\\Launcher.exe";

		public string RealExecutable => "opera.exe";
	
		public string PrivacyArgs => "--private ";
	
		public string PrivacyMode => "Open in private mode";
	}
}
