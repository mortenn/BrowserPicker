namespace BrowserPicker.Common;

/// <summary>
/// Controls how browser profiles are displayed in the picker UI.
/// </summary>
public enum ProfileDisplayMode
{
    /// <summary>Profiles shown as expandable sub-entries under their browser (default).</summary>
    Grouped,

    /// <summary>Each profile shown as a separate top-level entry (e.g. "Chrome - Work").</summary>
    Flat
}
