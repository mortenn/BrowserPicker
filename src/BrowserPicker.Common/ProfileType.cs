namespace BrowserPicker.Common;

/// <summary>
/// Identifies how profiles are discovered for a browser.
/// </summary>
public enum ProfileType
{
    /// <summary>No profile support or unknown browser.</summary>
    None,

    /// <summary>Chromium-based browser (Chrome, Edge, Vivaldi). Profiles live in a "User Data" directory.</summary>
    Chromium,

    /// <summary>Firefox-based browser. Profiles defined in profiles.ini; containers in containers.json.</summary>
    Firefox
}
