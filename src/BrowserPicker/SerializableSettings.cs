using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace BrowserPicker;

public sealed class SerializableSettings : IApplicationSettings
{
	public SerializableSettings(IApplicationSettings applicationSettings)
	{
		FirstTime = applicationSettings.FirstTime;
		AlwaysPrompt = applicationSettings.AlwaysPrompt;
		AlwaysUseDefaults = applicationSettings.AlwaysUseDefaults;
		AlwaysAskWithoutDefault = applicationSettings.AlwaysAskWithoutDefault;
		UrlLookupTimeoutMilliseconds = applicationSettings.UrlLookupTimeoutMilliseconds;
		UseAutomaticOrdering = applicationSettings.UseAutomaticOrdering;
		DisableTransparency = applicationSettings.DisableTransparency;
		DisableNetworkAccess = applicationSettings.DisableNetworkAccess;
		UrlShorteners = applicationSettings.UrlShorteners;
		BrowserList = [.. applicationSettings.BrowserList.Where(b => !b.Removed)];
		Defaults = [.. applicationSettings.Defaults.Where(d => !d.Deleted && !string.IsNullOrWhiteSpace(d.Browser))];
	}

	public SerializableSettings()
	{
	}

	public bool FirstTime { get; set; }
	public bool AlwaysPrompt { get; set; }
	public bool AlwaysUseDefaults { get; set; }
	public bool AlwaysAskWithoutDefault { get; set; }
	public int UrlLookupTimeoutMilliseconds { get; set; }
	public bool DisableTransparency { get; set; }
	public bool DisableNetworkAccess { get; set; }
	public string[] UrlShorteners { get; set; } = [];
	public List<BrowserModel> BrowserList { get; init; } = [];
	public List<DefaultSetting> Defaults { get; init; } = [];

	public SortOrder SortBy { get; set; }

	[JsonIgnore]
	public bool UseManualOrdering
	{
		get => SortBy == SortOrder.Manual;
		set
		{
			if (value)
			{
				SortBy = SortOrder.Manual;
			}
		}
	}

	[JsonIgnore]
	public bool UseAutomaticOrdering
	{
		get => SortBy == SortOrder.Automatic;
		set
		{
			if (value)
			{
				SortBy = SortOrder.Automatic;
			}
		}
	}

	[JsonIgnore]
	public bool UseAlphabeticalOrdering
	{
		get => SortBy == SortOrder.Alphabetical;
		set
		{
			if (value)
			{
				SortBy = SortOrder.Alphabetical;
			}
		}
	}

	public enum SortOrder
	{
		Automatic,
		Manual,
		Alphabetical
	}
}