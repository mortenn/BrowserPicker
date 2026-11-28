namespace BrowserPicker.UI.ViewModels;

/// <summary>
/// Why Browser Picker opened a link in a browser without showing the picker.
/// </summary>
public enum AutomaticLaunchReason
{
	/// <summary>No browser was chosen; the picker is shown.</summary>
	None,

	/// <summary>A rule on the Defaults tab matched the URL.</summary>
	Rule,

	/// <summary>No rule matched, and the fallback browser from Behaviour was used.</summary>
	Fallback,

	/// <summary>No rule matched, and exactly one browser was running.</summary>
	OnlyRunningBrowser,
}
