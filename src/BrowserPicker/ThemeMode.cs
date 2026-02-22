namespace BrowserPicker;

/// <summary>
/// Application theme preference: follow system, or force light or dark.
/// </summary>
public enum ThemeMode
{
	/// <summary>Use the current Windows theme (light or dark).</summary>
	System,

	/// <summary>Force light theme.</summary>
	Light,

	/// <summary>Force dark theme.</summary>
	Dark
}
