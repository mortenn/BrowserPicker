using System.ComponentModel;
using System.Diagnostics;
using BrowserPicker.Framework;

namespace BrowserPicker.ViewModel;

/// <summary>
/// ViewModel wrapping a <see cref="BrowserProfile"/> for display in the picker UI.
/// Provides launch commands that delegate to the parent <see cref="BrowserViewModel"/>.
/// </summary>
[DebuggerDisplay("{Model.Name}")]
public sealed class BrowserProfileViewModel : ViewModelBase<BrowserProfile>
{
    public BrowserProfileViewModel(BrowserProfile model, BrowserViewModel parent) : base(model)
    {
        this.parent = parent;
        parent.PropertyChanged += OnParentPropertyChanged;
    }

    private void OnParentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BrowserViewModel.AltPressed))
        {
            OnPropertyChanged(nameof(AltPressed));
        }
    }

    /// <summary>
    /// Launches the parent browser with this profile.
    /// </summary>
    public DelegateCommand Select => select ??= new DelegateCommand(
        () => parent.LaunchWithProfile(false, Model));

    /// <summary>
    /// Launches the parent browser with this profile in privacy mode.
    /// </summary>
    public DelegateCommand SelectPrivacy => select_privacy ??= new DelegateCommand(
        () => parent.LaunchWithProfile(true, Model));

    /// <summary>
    /// Display name combining the browser name and profile name, used in flat mode.
    /// </summary>
    public string FlatDisplayName => $"{parent.Model.Name} – {Model.Name}";

    /// <summary>
    /// The parent browser's icon path.
    /// </summary>
    public string? IconPath => parent.Model.IconPath;

    /// <summary>
    /// The parent browser's privacy tooltip.
    /// </summary>
    public string PrivacyTooltip => parent.PrivacyTooltip;

    /// <summary>
    /// Whether the parent browser has privacy mode args.
    /// </summary>
    public bool HasPrivacyMode => parent.Model.PrivacyArgs != null;

    /// <summary>
    /// Passthrough for Alt key state.
    /// </summary>
    public bool AltPressed => parent.AltPressed;

    private DelegateCommand? select;
    private DelegateCommand? select_privacy;
    private readonly BrowserViewModel parent;
}
