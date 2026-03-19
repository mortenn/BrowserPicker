using System.Collections.Generic;

namespace BrowserPicker;

/// <summary>
/// Application settings for the browser picker: prompts, defaults, browser list, URL shorteners, and key bindings.
/// </summary>
public interface IApplicationSettings
{
	/// <summary>
	/// First time the user launches the application
	/// </summary>
	bool FirstTime { get; set; }

	/// <summary>
	/// When set to true, disables the automatic selection of a browser
	/// </summary>
	bool AlwaysPrompt { get; set; }

	/// <summary>
	/// When set to false, disable launching the default browser as defined by url pattern if it is not running
	/// </summary>
	bool AlwaysUseDefaults { get; set; }

	/// <summary>
	/// When set to true and there is no matching default browser, the user choice prompt will be shown
	/// </summary>
	bool AlwaysAskWithoutDefault { get; set; }

	/// <summary>
	/// Timeout for resolving underlying url for an address
	/// </summary>
	int UrlLookupTimeoutMilliseconds { get; set; }

	/// <summary>
	/// How the browser list is ordered in the picker UI.
	/// </summary>
	SerializableSettings.SortOrder SortBy { get; set; }

	/// <summary>
	/// When set to true, lets user reorder the list of browsers manually.
	/// Convenience wrapper around <see cref="SortBy"/> for UI bindings.
	/// </summary>
	bool UseManualOrdering { get; set; }

	/// <summary>
	/// When set to true, orders the list of browsers based on popularity.
	/// Convenience wrapper around <see cref="SortBy"/> for UI bindings.
	/// </summary>
	bool UseAutomaticOrdering { get; set; }

	/// <summary>
	/// When set to true, orders the list of browsers alphabetically.
	/// Convenience wrapper around <see cref="SortBy"/> for UI bindings.
	/// </summary>
	bool UseAlphabeticalOrdering { get; set; }

	/// <summary>
	/// Use transparency for the popup window
	/// </summary>
	bool DisableTransparency { get; set; }

	/// <summary>
	/// Window opacity when transparency is enabled (0.0 = fully transparent, 1.0 = fully opaque). Clamped to 0.5–1.0.
	/// </summary>
	double WindowOpacity { get; set; }

	/// <summary>
	/// Disables all features that call out to the network
	/// </summary>
	bool DisableNetworkAccess { get; set; }

	/// <summary>
	/// List of host names known to be url shorteners
	/// </summary>
	string[] UrlShorteners { get; set; }

	/// <summary>
	/// The configured list of browsers
	/// </summary>
	List<BrowserModel> BrowserList { get; }

	/// <summary>
	/// Rules for per url browser defaults
	/// </summary>
	List<DefaultSetting> Defaults { get; }
	
	/// <summary>
	/// Manual keybindings
	/// </summary>
	List<KeyBinding> KeyBindings { get; }

	/// <summary>
	/// When true, the main window sizes to content. When false, <see cref="WindowWidth"/> and <see cref="WindowHeight"/> are used.
	/// </summary>
	bool AutoSizeWindow { get; set; }

	/// <summary>
	/// Preferred main window width in pixels. Used when <see cref="AutoSizeWindow"/> is false.
	/// </summary>
	double WindowWidth { get; set; }

	/// <summary>
	/// Preferred main window height in pixels. Used when <see cref="AutoSizeWindow"/> is false.
	/// </summary>
	double WindowHeight { get; set; }

	/// <summary>
	/// Configuration window width in pixels. Persisted separately from main window; config mode never uses auto size.
	/// </summary>
	double ConfigWindowWidth { get; set; }

	/// <summary>
	/// Configuration window height in pixels. Persisted separately from main window; config mode never uses auto size.
	/// </summary>
	double ConfigWindowHeight { get; set; }

	/// <summary>
	/// Base font size in pixels for the picker and settings UI.
	/// </summary>
	double FontSize { get; set; }

	/// <summary>
	/// Theme preference: system, light, or dark.
	/// </summary>
	ThemeMode ThemeMode { get; set; }

	/// <summary>
	/// How browser profiles are displayed in the picker UI: grouped under their browser or as flat top-level entries.
	/// </summary>
	ProfileDisplayMode ProfileDisplayMode { get; set; }

}