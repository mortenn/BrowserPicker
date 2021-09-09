using System;
using System.Collections.Generic;
namespace BrowserPicker
{
	public interface IBrowserPickerConfiguration
	{

		bool AlwaysPrompt { get; set; }
		bool DefaultsWhenRunning { get; set; }
		int UrlLookupTimeoutMilliseconds { get; set; }
		DateTime LastBrowserScanTime { get; set; }
		bool UseAutomaticOrdering { get; set; }
		bool DisableTransparency { get; set; }
		bool DisableNetworkAccess { get; set; }

		List<BrowserModel> BrowserList { get; }
		void AddBrowser(BrowserModel browser);
		void FindBrowsers();

		List<DefaultSetting> Defaults { get; }
		DefaultSetting AddDefault(string fragment, string browser);
	}
}
