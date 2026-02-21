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
	/// When set to true, lets user reorder the list of browsers manually
	/// </summary>
	/// <remarks>Mutually exclusive with <see cref="UseAutomaticOrdering"/> and <see cref="UseAlphabeticalOrdering"/></remarks>
	bool UseManualOrdering { get; set; }

	/// <summary>
	/// When set to false, stops reordering the list of browsers based on popularity
	/// </summary>
	/// <remarks>Mutually exclusive with <see cref="UseManualOrdering"/> and <see cref="UseAlphabeticalOrdering"/></remarks>
	bool UseAutomaticOrdering { get; set; }

	/// <summary>
	/// When set to false, orders the list of browsers based on alphabetical order
	/// </summary>
	/// <remarks>Mutually exclusive with <see cref="UseAutomaticOrdering"/> and <see cref="UseManualOrdering"/></remarks>
	bool UseAlphabeticalOrdering { get; set; }

	/// <summary>
	/// Use transparency for the popup window
	/// </summary>
	bool DisableTransparency { get; set; }

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
}