using System.Collections.Generic;
using System.ComponentModel;

namespace BrowserPicker
{
	public interface IBrowserPickerConfiguration : INotifyPropertyChanged, ILongRunningProcess
	{
		/// <summary>
		/// When set to true, disables the automatic selection of a browser
		/// </summary>
		bool AlwaysPrompt { get; set; }

		/// <summary>
		/// When set to false, disable launching the default browser as defined by url pattern if it is not running
		/// </summary>
		bool DefaultsWhenRunning { get; set; }

		/// <summary>
		/// Timeout for resolving underlying url for an address
		/// </summary>
		int UrlLookupTimeoutMilliseconds { get; set; }

		/// <summary>
		/// When set to false, stops reordering the list of browsers based on popularity
		/// </summary>
		bool UseAutomaticOrdering { get; set; }

		/// <summary>
		/// Use transparency for the popup window
		/// </summary>
		bool DisableTransparency { get; set; }

		/// <summary>
		/// Disables all features that call out to the network
		/// </summary>
		bool DisableNetworkAccess { get; set; }

		/// <summary>
		/// The configured list of browsers
		/// </summary>
		List<BrowserModel> BrowserList { get; }

		/// <summary>
		/// Add a new browser to the list
		/// </summary>
		void AddBrowser(BrowserModel browser);

		/// <summary>
		/// Scan the system for known browsers
		/// </summary>
		void FindBrowsers();

		/// <summary>
		/// Rules for per url browser defaults
		/// </summary>
		List<DefaultSetting> Defaults { get; }

		/// <summary>
		/// When true, only urls matching some <see cref="Defaults"/> record will give the user a choice.
		/// This makes BrowserPicker only seemingly apply for certain urls.
		/// </summary>
		public bool AlwaysUseDefault { get; set; }

		/// <summary>
		/// The browser to use if <see cref="AlwaysUseDefault"/> is true and no <see cref="Defaults"/> match the url.
		/// </summary>
		public string DefaultBrowser { get; set; }

		/// <summary>
		/// Add a default setting rule to the configuration
		/// </summary>
		/// <param name="fragment">The url fragment to match</param>
		/// <param name="browser">The browser to use</param>
		DefaultSetting AddDefault(string fragment, string browser);
	}
}
