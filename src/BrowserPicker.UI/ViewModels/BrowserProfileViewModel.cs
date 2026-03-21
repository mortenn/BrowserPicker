using System.ComponentModel;
using System.Diagnostics;
using BrowserPicker.Common;
using BrowserPicker.Common.Framework;

namespace BrowserPicker.UI.ViewModels;

/// <summary>
/// ViewModel wrapping a <see cref="BrowserProfile"/> for display in the picker UI.
/// Provides launch commands that delegate to the parent <see cref="BrowserViewModel"/>.
/// </summary>
[DebuggerDisplay("{Model.Name}")]
public sealed class BrowserProfileViewModel : ViewModelBase<BrowserProfile>
{
	public BrowserProfileViewModel(BrowserProfile model, BrowserViewModel parentBrowser) : base(model)
	{
		ParentBrowser = parentBrowser;
		ParentBrowser.PropertyChanged += OnParentPropertyChanged;
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
		() => ParentBrowser.LaunchWithProfile(false, Model));

	/// <summary>
	/// Launches the parent browser with this profile in privacy mode.
	/// </summary>
	public DelegateCommand SelectPrivacy => select_privacy ??= new DelegateCommand(
		() => ParentBrowser.LaunchWithProfile(true, Model));

	/// <summary>
	/// Display name combining the browser name and profile name, used in flat mode.
	/// </summary>
	public string FlatDisplayName => $"{ParentBrowser.Model.Name} – {Model.Name}";

	/// <summary>
	/// The browser this profile belongs to (flat picker rows mirror <see cref="BrowserViewModel.IsRunning"/>).
	/// </summary>
	public BrowserViewModel ParentBrowser { get; }

	/// <summary>
	/// True when the parent browser has a running main window in this session.
	/// </summary>
	public bool IsRunning => ParentBrowser.IsRunning;

	/// <summary>
	/// The parent browser's icon path.
	/// </summary>
	public string? IconPath => ParentBrowser.Model.IconPath;

	/// <summary>
	/// The parent browser's privacy tooltip.
	/// </summary>
	public string PrivacyTooltip => ParentBrowser.PrivacyTooltip;

	/// <summary>
	/// Whether the parent browser has privacy mode args.
	/// </summary>
	public bool HasPrivacyMode => ParentBrowser.Model.PrivacyArgs != null;

	/// <summary>
	/// Pass-through for Alt key state.
	/// </summary>
	public bool AltPressed => ParentBrowser.AltPressed;

	private DelegateCommand? select;
	private DelegateCommand? select_privacy;
}
