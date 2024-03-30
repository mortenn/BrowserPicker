using System.Collections.Generic;
using System.ComponentModel;

namespace BrowserPicker
{
	public interface IBrowserPickerConfiguration : INotifyPropertyChanged, ILongRunningProcess
	{

		bool AlwaysPrompt { get; set; }
		bool DefaultsWhenRunning { get; set; }
		int UrlLookupTimeoutMilliseconds { get; set; }
		bool UseAutomaticOrdering { get; set; }
		bool DisableTransparency { get; set; }
		bool DisableNetworkAccess { get; set; }

		List<BrowserModel> BrowserList { get; }
		void AddBrowser(BrowserModel browser);
		void FindBrowsers();

		List<DefaultSetting> Defaults { get; }
		public bool AlwaysUseDefault { get; set; }
		public string DefaultBrowser { get; set; }
		DefaultSetting AddDefault(string fragment, string browser);
	}
}
