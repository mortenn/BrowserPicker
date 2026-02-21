namespace BrowserPicker;

/// <summary>
/// Associates a keyboard key with a browser for quick launch.
/// </summary>
/// <param name="Key">The key string (e.g. "1", "2") that triggers this browser.</param>
/// <param name="Browser">The browser id of the browser to launch when the key is pressed.</param>
public record KeyBinding(string Key, string Browser);