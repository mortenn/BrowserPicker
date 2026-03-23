using System;
using System.Diagnostics;
using JetBrains.Annotations;
using BrowserPicker.Common.Framework;

namespace BrowserPicker.Common;

/// <summary>
/// Represents a single profile within a browser (e.g. a Chrome profile directory, Firefox profile, or Firefox container).
/// </summary>
[DebuggerDisplay("{" + nameof(Name) + "}"), PublicAPI]
public sealed class BrowserProfile(string id, string name, string? commandArgs, string? urlTemplate = null)
    : ModelBase
{
    [UsedImplicitly]
    public BrowserProfile() : this(string.Empty, string.Empty, null)
    {
    }

    /// <summary>
    /// Stable identifier for this profile (e.g. "Default", "Profile 1", "container:Work").
    /// </summary>
    public string Id
    {
        get => id;
        private set => _ = SetProperty(ref id, value);
    }

    /// <summary>
    /// Display name shown in the picker UI (e.g. "Personal", "Work").
    /// </summary>
    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    /// <summary>
    /// Additional command-line arguments for this profile, prepended before the URL.
    /// For Chromium: <c>--profile-directory="Profile 1"</c>. For Firefox: <c>-P "Work"</c>.
    /// </summary>
    public string? CommandArgs
    {
        get => command_args;
        set => SetProperty(ref command_args, value);
    }

    /// <summary>
    /// Optional URL transformation template. When set, <c>{url}</c> is replaced with the actual URL.
    /// Used for Firefox containers: <c>ext+container:name=Work&amp;url={url}</c>.
    /// Null means the URL is passed through unchanged.
    /// </summary>
    public string? UrlTemplate
    {
        get => url_template;
        set => SetProperty(ref url_template, value);
    }

    /// <summary>
    /// Number of times this profile has been used (for automatic sorting).
    /// </summary>
    public int Usage
    {
        get => usage;
        set => SetProperty(ref usage, value);
    }

    /// <summary>
    /// When true, this profile is hidden from the picker UI.
    /// </summary>
    public bool Disabled
    {
        get => disabled;
        private set => _ = SetProperty(ref disabled, value);
    }

    /// <summary>
    /// Firefox container color name (e.g. "blue", "orange", "green").
    /// Null for non-container profiles.
    /// </summary>
    public string? IconColor
    {
        get => icon_color;
        set => SetProperty(ref icon_color, value);
    }

    /// <summary>
    /// Firefox container icon name (e.g. "fingerprint", "briefcase", "circle").
    /// Null for non-container profiles.
    /// </summary>
    public string? ContainerIcon
    {
        get => container_icon;
        set => SetProperty(ref container_icon, value);
    }

    /// <summary>
    /// Applies the URL template transformation if one is configured.
    /// The target URL is percent-encoded so that <c>&amp;</c> characters in it
    /// do not break the template's own parameter parsing.
    /// </summary>
    public string TransformUrl(string url)
    {
        return url_template != null ? url_template.Replace("{url}", Uri.EscapeDataString(url)) : url;
    }

    private string id = id;
    private string name = name;
    private string? command_args = commandArgs;
    private string? url_template = urlTemplate;
    private int usage;
    private bool disabled;
    private string? icon_color;
    private string? container_icon;
}
