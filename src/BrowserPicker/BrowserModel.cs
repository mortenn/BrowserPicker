using BrowserPicker.Framework;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace BrowserPicker;

/// <summary>
/// Represents a model for a browser, containing details about its name, command, executable path, icon, and other settings.
/// </summary>
[DebuggerDisplay("{" + nameof(Name) + "}")]
public sealed class BrowserModel : ModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BrowserModel"/> class with default values.
    /// </summary>
    public BrowserModel()
    {
        name = string.Empty;
        command = string.Empty;
        id = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BrowserModel"/> class using a well-known browser configuration.
    /// </summary>
    /// <param name="known">The known browser instance containing default browser properties.</param>
    /// <param name="icon">The path to the browser's icon.</param>
    /// <param name="shell">The shell command used to launch the browser.</param>
    public BrowserModel(IWellKnownBrowser known, string? icon, string shell)
    {
        name = known.Name;
        id = known.Name;
        command = shell;
        PrivacyArgs = known.PrivacyArgs;
        Executable = known.RealExecutable;
        IconPath = icon;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BrowserModel"/> class with specified values.
    /// </summary>
    /// <param name="name">The name of the browser.</param>
    /// <param name="icon">The path to the browser's icon.</param>
    /// <param name="shell">The shell command used to launch the browser.</param>
    public BrowserModel(string name, string? icon, string shell)
    {
        this.name = name;
        id = name;
        icon_path = icon;
        command = shell;
    }

    /// <summary>
    /// Stable identifier used as the registry key; does not change when the user renames the browser.
    /// Persisted in JSON so defaults and key bindings survive renames and restart.
    /// </summary>
    public string Id
    {
        get => id;
        set => SetProperty(ref id, value);
    }

    /// <summary>
    /// Gets or sets the name of the browser.
    /// </summary>
    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    /// <summary>
    /// Gets or sets the path to the browser's icon.
    /// </summary>
    public string? IconPath
    {
        get => icon_path;
        set => SetProperty(ref icon_path, value);
    }

    /// <summary>
    /// Gets or sets the shell command used to launch the browser.
    /// </summary>
    public string Command
    {
        get => command;
        set => SetProperty(ref command, value);
    }

    /// <summary>
    /// Gets or sets the path to the browser executable.
    /// </summary>
    public string? Executable
    {
        get => executable;
        set => SetProperty(ref executable, value);
    }

    /// <summary>
    /// Gets or sets additional arguments to provide when launching a URL.
    /// </summary>
    public string? CommandArgs
    {
        get => command_args;
        set => SetProperty(ref command_args, value);
    }

    /// <summary>
    /// Gets or sets additional arguments to launch a URL in private browsing mode.
    /// </summary>
    public string? PrivacyArgs
    {
        get => privacy_args;
        set => SetProperty(ref privacy_args, value);
    }

    /// <summary>
    /// Gets or sets the usage count of the browser.
    /// </summary>
    public int Usage
    {
        get => usage;
        set => SetProperty(ref usage, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the browser is disabled.
    /// </summary>
    public bool Disabled
    {
        get => disabled;
        set => SetProperty(ref disabled, value);
    }

    /// <summary>
    /// Flag set when the user deletes a browser in the GUI.
    /// </summary>
    public bool Removed
    {
        get => removed;
        set
        {
            removed = value;
            Disabled = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the order of the browser in manual sorting mode.
    /// </summary>
    public int ManualOrder
    {
        get => manual_order;
        set => SetProperty(ref manual_order, value);
    }

    /// <summary>
    /// Gets or sets a value determining whether "file://" URLs should be converted to
    /// regular UNC/local paths before launching them with this browser.
    /// </summary>
    public bool ExpandFileUrls
    {
        get => expand_file_url;
        set => SetProperty(ref expand_file_url, value);
    }

    /// <summary>
    /// Disables or enables updating the browser definition through browser detection.
    /// </summary>
    public bool ManualOverride
    {
        get => manual_override;
        set => SetProperty(ref manual_override, value);
    }

    /// <summary>
    /// Gets or sets a custom keybinding to launch the browser.
    /// </summary>
    [JsonIgnore]
    public string CustomKeyBind
    {
        get => custom_key;
        set => SetProperty(ref custom_key, value);
    }

    private int usage;
    private bool disabled;
    private bool removed;
    private string id;
    private string name;
    private string? icon_path;
    private string command;
    private string? executable;
    private string? command_args;
    private string? privacy_args;
    private int manual_order;
    private bool expand_file_url;
    private bool manual_override;
    private string custom_key = string.Empty;
}