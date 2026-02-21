using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace BrowserPicker;

/// <summary>
/// Serializable snapshot of application settings used for backup/restore and JSON export/import.
/// </summary>
public sealed class SerializableSettings : IApplicationSettings
{
	/// <summary>
	/// Initializes a new instance by copying from an existing <see cref="IApplicationSettings"/> instance.
	/// Excludes removed browsers, deleted defaults, and key bindings for removed browsers.
	/// </summary>
	/// <param name="applicationSettings">The source settings to copy from.</param>
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
		KeyBindings = applicationSettings.KeyBindings
			.Where(kb => applicationSettings.BrowserList.Any(b => (b.Id == kb.Browser || b.Name == kb.Browser) && !b.Removed))
			.ToList();
	}

	/// <summary>
	/// Parameterless constructor for JSON deserialization.
	/// </summary>
	public SerializableSettings()
	{
	}

	/// <inheritdoc />
	public bool FirstTime { get; set; }
	/// <inheritdoc />
	public bool AlwaysPrompt { get; set; }
	/// <inheritdoc />
	public bool AlwaysUseDefaults { get; set; }
	/// <inheritdoc />
	public bool AlwaysAskWithoutDefault { get; set; }
	/// <inheritdoc />
	public int UrlLookupTimeoutMilliseconds { get; set; }
	/// <inheritdoc />
	public bool DisableTransparency { get; set; }
	/// <inheritdoc />
	public bool DisableNetworkAccess { get; set; }
	/// <inheritdoc />
	public string[] UrlShorteners { get; set; } = [];
	/// <inheritdoc />
	public List<BrowserModel> BrowserList { get; init; } = [];
	/// <inheritdoc />
	public List<DefaultSetting> Defaults { get; init; } = [];
	/// <inheritdoc />
	public List<KeyBinding> KeyBindings { get; init; } = [];

	/// <summary>
	/// How to sort the browser list: automatic (by usage), manual, or alphabetical.
	/// </summary>
	public SortOrder SortBy { get; set; }

	/// <inheritdoc />
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

	/// <inheritdoc />
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

	/// <inheritdoc />
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

	/// <summary>
	/// Defines how the browser list is ordered in the picker UI.
	/// </summary>
	public enum SortOrder
	{
		/// <summary>Order by usage (most used first).</summary>
		Automatic,
		/// <summary>Order by user-defined manual order.</summary>
		Manual,
		/// <summary>Order alphabetically by browser name.</summary>
		Alphabetical
	}
}