using System.Collections.Generic;
using System.Linq;

namespace BrowserPicker.Windows;

public sealed class SerializableSettings : IApplicationSettings
{
	public SerializableSettings(IApplicationSettings applicationSettings)
	{
		AlwaysPrompt = applicationSettings.AlwaysPrompt;
		AlwaysUseDefaults = applicationSettings.AlwaysUseDefaults;
		AlwaysAskWithoutDefault = applicationSettings.AlwaysAskWithoutDefault;
		UrlLookupTimeoutMilliseconds = applicationSettings.UrlLookupTimeoutMilliseconds;
		UseAutomaticOrdering = applicationSettings.UseAutomaticOrdering;
		DisableTransparency = applicationSettings.DisableTransparency;
		DisableNetworkAccess = applicationSettings.DisableNetworkAccess;
		BrowserList = [.. applicationSettings.BrowserList.Where(b => !b.Removed)];
		Defaults = [.. applicationSettings.Defaults.Where(d => !d.Deleted && !string.IsNullOrWhiteSpace(d.Browser))];
	}

	public SerializableSettings()
	{
	}
	
	public bool AlwaysPrompt { get; set; }
	public bool AlwaysUseDefaults { get; set; }
	public bool AlwaysAskWithoutDefault { get; set; }
	public int UrlLookupTimeoutMilliseconds { get; set; }
	public bool UseAutomaticOrdering { get; set; }
	public bool DisableTransparency { get; set; }
	public bool DisableNetworkAccess { get; set; }
	public List<BrowserModel> BrowserList { get; set; } = [];
	public List<DefaultSetting> Defaults { get; set; } = [];
}