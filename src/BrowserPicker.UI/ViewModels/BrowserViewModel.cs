using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using BrowserPicker.Common;
using BrowserPicker.Common.Framework;
using BrowserPicker.UI.Views;

#if DEBUG
using JetBrains.Annotations;
#endif

namespace BrowserPicker.UI.ViewModels;

/// <summary>
/// ViewModel class for representing and interacting with a browser in the application.
/// Encapsulates logic for managing browser configurations and commands.
/// </summary>
[DebuggerDisplay("{" + nameof(Model) + "." + nameof(BrowserModel.Name) + "}")]
public sealed class BrowserViewModel : ViewModelBase<BrowserModel>
{
	private const string MozillaContainerExtensionLaunchArgs =
		"\"https://addons.mozilla.org/en-US/firefox/addon/open-url-in-container/\"";

#if DEBUG
	/// <summary>
	/// Parameterless constructor for WPF Designer support during debugging.
	/// Initializes the ViewModel with default values.
	/// </summary>
	[UsedImplicitly]
	public BrowserViewModel() : base(new BrowserModel
	{
		Name = "Google Chrome",
		Profiles =
		{
			new BrowserProfile("Default", "Personal", """--profile-directory="Default" """),
			new BrowserProfile("Profile 1", "Work", """--profile-directory="Profile 1" """)
		}
	})
	{
		parent_view_model = new ApplicationViewModel();
	}
#endif

	/// <summary>
	/// Initializes a new instance of the <see cref="BrowserViewModel"/> class with the specified browser model and parent ViewModel.
	/// </summary>
	/// <param name="model">The browser model associated with this ViewModel.</param>
	/// <param name="viewModel">The parent application ViewModel.</param>
	public BrowserViewModel(BrowserModel model, ApplicationViewModel viewModel) : base(model)
	{
		model.PropertyChanged += Model_PropertyChanged;
		parent_view_model = viewModel;
		parent_view_model.PropertyChanged += OnParentViewModelChanged;
		parent_view_model.Configuration.Settings.PropertyChanged += Settings_PropertyChanged;
	}

	/// <summary>
	/// Listens to property changed events to detect switching between automatic and manual ordering.
	/// </summary>
	private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(IApplicationSettings.SortBy):
				OnPropertyChanged(nameof(IsManuallyOrdered));
				break;
			case nameof(IApplicationSettings.ProfileDisplayMode):
				OnPropertyChanged(nameof(ShowNestedProfilesInPicker));
				OnPropertyChanged(nameof(ShowNestedProfileSubtree));
				break;
		}
	}

	/// <summary>
	/// Listens to property changed events to detect the user holding down the Alt key.
	/// </summary>
	private void OnParentViewModelChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(ApplicationViewModel.AltPressed))
		{
			OnPropertyChanged(nameof(AltPressed));
		}
	}

	/// <summary>
	/// Handles changes in the browser model's properties and updates command states accordingly.
	/// </summary>
	private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(BrowserModel.Id):
			case nameof(BrowserModel.Name):
				OnPropertyChanged(nameof(IsWellKnown));
				OnPropertyChanged(nameof(CanRemove));
				Remove.RaiseCanExecuteChanged();
				break;

			case nameof(BrowserModel.PrivacyArgs):
				SelectPrivacy.RaiseCanExecuteChanged();
				break;

			case nameof(BrowserModel.Disabled):
				SelectPrivacy.RaiseCanExecuteChanged();
				Select.RaiseCanExecuteChanged();
				break;

			case nameof(BrowserModel.ContainersEnabled):
				RefreshProfiles();
				break;
		}
	}

	private void RefreshProfiles()
	{
		profile_view_models = null;
		IsExpanded = false;
		OnPropertyChanged(nameof(ProfileViewModels));
		OnPropertyChanged(nameof(HasProfiles));
		OnPropertyChanged(nameof(ShowNestedProfilesInPicker));
		OnPropertyChanged(nameof(ShowNestedProfileSubtree));
		parent_view_model.RebuildPickerChoices();
	}

	/// <summary>
	/// Gets a value indicating whether the browser list is ordered manually.
	/// </summary>
	public bool IsManuallyOrdered => parent_view_model.Configuration.Settings.SortBy == SerializableSettings.SortOrder.Manual;

	/// <summary>
	/// Gets a value indicating whether this browser came from the built-in well-known browser list.
	/// </summary>
	public bool IsWellKnown => WellKnownBrowsers.Lookup(string.IsNullOrWhiteSpace(Model.Id) ? Model.Name : Model.Id, null) != null;

	/// <summary>
	/// Gets a value indicating whether this browser can be permanently removed from the configuration.
	/// </summary>
	public bool CanRemove => !IsWellKnown;

	/// <summary>
	/// Gets the command to select the browser.
	/// </summary>
	public DelegateCommand Select => select ??= new DelegateCommand(() => Launch(false), () => CanLaunch(false));

	/// <summary>
	/// Gets the command to select the browser with privacy mode enabled.
	/// </summary>
	public DelegateCommand SelectPrivacy => select_privacy ??= new DelegateCommand(() => Launch(true), () => CanLaunch(true));

	/// <summary>
	/// Gets the command to enable or disable the browser.
	/// </summary>
	public DelegateCommand Disable => disable ??= new DelegateCommand(() => Model.Disabled = !Model.Disabled);

	/// <summary>
	/// Gets the command to remove the browser from the list.
	/// </summary>
	public DelegateCommand Remove => remove ??= new DelegateCommand(() => Model.Removed = true, () => CanRemove);

	/// <summary>
	/// Gets the command to open the browser editor.
	/// </summary>
	public DelegateCommand Edit => edit ??= new DelegateCommand(() => OpenEditor(Model));

	/// <summary>
	/// Gets the command to duplicate this browser, opening the new-browser dialog with settings copied from this one.
	/// </summary>
	public DelegateCommand Duplicate => duplicate ??= new DelegateCommand(PerformDuplicate);

	/// <summary>
	/// Gets the command to move the browser up in the list.
	/// </summary>
	public DelegateCommand MoveUp => move_up ??= new DelegateCommand(() => Swap(1), () => CanSwap(1));

	/// <summary>
	/// Gets the command to move the browser down in the list.
	/// </summary>
	public DelegateCommand MoveDown => move_down ??= new DelegateCommand(() => Swap(-1), () => CanSwap(-1));

	private DelegateCommand? select;
	private DelegateCommand? select_privacy;
	private DelegateCommand? disable;
	private DelegateCommand? remove;
	private DelegateCommand? edit;
	private DelegateCommand? duplicate;
	private DelegateCommand? move_up;
	private DelegateCommand? move_down;

	/// <summary>
	/// Determines if the browser can be swapped with another based on the specified offset.
	/// </summary>
	/// <param name="offset">The offset to check for swapping (e.g., -1 for upward, +1 for downward).</param>
	/// <returns>True if the browser can be swapped; otherwise, false.</returns>
	private bool CanSwap(int offset)
	{
		var choices = parent_view_model.Choices.Where(vm => !vm.Model.Removed).ToList();
		var i = choices.IndexOf(this);
		var ni = i - offset;
		return ni >= 0 && ni < choices.Count;
	}

	/// <summary>
	/// Swaps the browser's position in the list with another browser based on the specified offset.
	/// </summary>
	/// <param name="offset">The offset indicating the direction and position for swapping.</param>
	private void Swap(int offset)
	{
		foreach (var choice in parent_view_model.Choices)
		{
			choice.Model.ManualOrder = parent_view_model.Choices.IndexOf(choice);
		}
		var i = parent_view_model.Choices.IndexOf(this) - offset;
		var next = parent_view_model.Choices[i];
		(next.Model.ManualOrder, Model.ManualOrder) = (Model.ManualOrder, next.Model.ManualOrder);
		parent_view_model.RefreshChoices();
	}

	/// <summary>
	/// Duplicates this browser by opening the new-browser dialog with settings copied from the current one.
	/// On OK, the new browser is added to the list; on Cancel, nothing is added.
	/// </summary>
	private void PerformDuplicate()
	{
		var clone = new BrowserModel
		{
			Name = string.Empty,
			Command = Model.Command,
			CommandArgs = Model.CommandArgs,
			Executable = Model.Executable,
			IconPath = Model.IconPath,
			PrivacyArgs = Model.PrivacyArgs,
			CustomKeyBind = string.Empty,
			ManualOverride = Model.ManualOverride,
			Disabled = false,
			Usage = 0,
			ExpandFileUrls = Model.ExpandFileUrls,
			ContainersEnabled = Model.ContainersEnabled
		};
		var editorVm = new BrowserViewModel(clone, parent_view_model);
		var editor = new BrowserEditor(editorVm);
		editor.Closing += OnDuplicateEditorClosing;
		editor.Show();
		return;

		void OnDuplicateEditorClosing(object? sender, CancelEventArgs e)
		{
			if (sender is Window window)
			{
				window.Closing -= OnDuplicateEditorClosing;
			}
			if (sender is not Window { DataContext: BrowserViewModel browser })
			{
				return;
			}
			if (string.IsNullOrEmpty(browser.Model.Name) || string.IsNullOrEmpty(browser.Model.Command))
			{
				return;
			}
			parent_view_model.Choices.Add(browser);
			parent_view_model.Configuration.Settings.AddBrowser(browser.Model);
		}
	}

	/// <summary>
	/// Opens the browser editor for modifying the given browser model.
	/// </summary>
	/// <param name="model">The browser model to be edited.</param>
	private void OpenEditor(BrowserModel model)
	{
		var temp = new BrowserModel
		{
			Id = model.Id,
			Command = Model.Command,
			CommandArgs = model.CommandArgs,
			Executable = model.Executable,
			IconPath = model.IconPath,
			Name = model.Name,
			PrivacyArgs = model.PrivacyArgs,
			CustomKeyBind = model.CustomKeyBind,
			ManualOverride = model.ManualOverride,
			Disabled = model.Disabled,
			ManualOrder = model.ManualOrder,
			Usage = model.Usage,
			ExpandFileUrls = model.ExpandFileUrls,
			ContainersEnabled = model.ContainersEnabled
		};
		var editor = new BrowserEditor(new BrowserViewModel(temp, parent_view_model));
		editor.Show();
		editor.Closing += Editor_Closing;
	}

	/// <summary>
	/// Handles the closing event of the browser editor and saves changes to the model if applicable.
	/// </summary>
	/// <param name="sender">The sender of the event (expected to be a <see cref="BrowserEditor"/>).</param>
	/// <param name="e">The event arguments for the closing event.</param>
	private void Editor_Closing(object? sender, CancelEventArgs e)
	{
		if ((sender as BrowserEditor)?.DataContext is not BrowserViewModel context)
		{
			return;
		}

		var save = context.Model;

		Model.Command = save.Command;
		Model.CommandArgs = save.CommandArgs;
		Model.IconPath = save.IconPath;
		Model.Name = save.Name;
		Model.Executable = save.Executable;
		Model.PrivacyArgs = save.PrivacyArgs;
		Model.ExpandFileUrls = save.ExpandFileUrls;
		Model.CustomKeyBind = save.CustomKeyBind;
		Model.ManualOverride = save.ManualOverride;
		Model.Disabled = save.Disabled;
		Model.ContainersEnabled = save.ContainersEnabled;

		parent_view_model.Configuration.Settings.PersistBrowser(Model);
		parent_view_model.RebuildPickerChoices();
	}

	/// <summary>
	/// Tooltip text for the privacy-mode action (e.g. "Open incognito"), from well-known browser or default.
	/// </summary>
	public string PrivacyTooltip
	{
		get
		{
			var known = WellKnownBrowsers.Lookup(Model.Name, null);
			return known?.PrivacyMode ?? "Open in privacy mode";
		}
	}

	/// <summary>
	/// True when a process for this browser is running in the current session with a main window.
	/// </summary>
	public bool IsRunning
	{
		get
		{
			try
			{
				var session = Process.GetCurrentProcess().SessionId;

				if (Model.Command == "microsoft-edge:" || Model.Command.Contains("MicrosoftEdge"))
					return Process.GetProcessesByName("MicrosoftEdge").Any(p => p.SessionId == session);

				string target;
				switch (Model.Executable)
				{
					case null:
						var cmd = Model.Command;
						if (cmd[0] == '"')
							cmd = cmd.Split('"')[1];

						target = cmd;
						break;

					default:
						target = Model.Executable;
						break;
				}

				return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(target))
					.Any(p => p.SessionId == session && p.MainWindowHandle != 0 && p.MainModule?.FileName == target);
			}
			catch
			{
				// Design time exceptions
				return false;
			}
		}
	}

	/// <summary>
	/// True when the user is holding the Alt key (e.g. to signal privacy mode).
	/// </summary>
	public bool AltPressed => parent_view_model.AltPressed;

	/// <summary>
	/// Whether this browser has any non-disabled profiles.
	/// </summary>
	public bool HasProfiles => Model.Profiles.Any(p => !p.Disabled);

	/// <summary>
	/// True when the picker should show the chevron and nested profile list (grouped mode only).
	/// </summary>
	public bool ShowNestedProfilesInPicker =>
		HasProfiles && parent_view_model.Configuration.Settings.ProfileDisplayMode == ProfileDisplayMode.Grouped;

	/// <summary>
	/// True when nested profiles are visible under the browser row.
	/// </summary>
	public bool ShowNestedProfileSubtree => ShowNestedProfilesInPicker && IsExpanded;

	/// <summary>
	/// Whether this browser supports Firefox-style containers (based on well-known browser type).
	/// </summary>
	public bool SupportsContainers
	{
		get
		{
			var known = WellKnownBrowsers.Lookup(
				string.IsNullOrWhiteSpace(Model.Id) ? Model.Name : Model.Id,
				Model.Executable ?? Model.Command);
			return known?.ProfileType == ProfileType.Firefox;
		}
	}

	/// <summary>
	/// Opens the extension install page using this browser.
	/// </summary>
	public DelegateCommand OpenExtensionLink => open_extension_link ??= new DelegateCommand(LaunchExtensionPage);

	private void LaunchExtensionPage()
	{
		try
		{
			var process = new ProcessStartInfo(Model.Command, MozillaContainerExtensionLaunchArgs) { UseShellExecute = false };
			Process.Start(process);
		}
		catch
		{
			// ignored
		}
	}

	/// <summary>
	/// Whether the profile sub-list is expanded in the picker UI (grouped mode).
	/// </summary>
	public bool IsExpanded
	{
		get => is_expanded;
		set
		{
			if (!SetProperty(ref is_expanded, value))
			{
				return;
			}

			OnPropertyChanged(nameof(ShowNestedProfileSubtree));
		}
	}

	/// <summary>
	/// Toggle expand/collapse of the profile sub-list.
	/// </summary>
	public DelegateCommand ExpandToggle => expand_toggle ??= new DelegateCommand(() => IsExpanded = !IsExpanded);

	/// <summary>
	/// Observable collection of profile view models for the picker UI.
	/// Lazily populated on first access.
	/// </summary>
	public ObservableCollection<BrowserProfileViewModel> ProfileViewModels
	{
		get
		{
			profile_view_models ??= new ObservableCollection<BrowserProfileViewModel>(
				Model.Profiles.Where(p => !p.Disabled).Select(p => new BrowserProfileViewModel(p, this)));
			return profile_view_models;
		}
	}

	private bool is_expanded;
	private DelegateCommand? expand_toggle;
	private DelegateCommand? open_extension_link;
	private ObservableCollection<BrowserProfileViewModel>? profile_view_models;

	/// <summary>
	/// Determines if the browser can be launched with the specified privacy setting.
	/// </summary>
	/// <param name="privacy">Whether the browser should be launched in privacy mode.</param>
	/// <returns>True if the browser can be launched; otherwise, false.</returns>
	private bool CanLaunch(bool privacy)
	{
		return !string.IsNullOrWhiteSpace(parent_view_model.Url.TargetURL) && !(privacy && Model.PrivacyArgs == null);
	}

	/// <summary>
	/// Launches the browser with the specified profile and privacy mode.
	/// Called by <see cref="BrowserProfileViewModel"/> and auto-selection.
	/// </summary>
	internal void LaunchWithProfile(bool privacy, BrowserProfile? profile)
	{
		Launch(privacy, profile);
	}

	private void Launch(bool privacy, BrowserProfile? profile = null)
	{
		if (parent_view_model.Url.TargetURL == null)
		{
			return;
		}
		try
		{
			if (App.Settings?.SortBy == SerializableSettings.SortOrder.Automatic)
			{
				Model.Usage++;
				if (profile != null)
				{
					profile.Usage++;
				}
			}

			parent_view_model.Configuration.UrlOpened(parent_view_model.Url.HostName, Model.Id);

			var profileArgs = profile?.CommandArgs ?? string.Empty;
			var newArgs = privacy ? Model.PrivacyArgs : string.Empty;
			var rawUrl = parent_view_model.Url.GetTargetUrl(Model.ExpandFileUrls) ?? parent_view_model.Url.TargetURL!;
			var url = profile != null ? profile.TransformUrl(rawUrl) : rawUrl;
			var args = CombineArgs(Model.CommandArgs, CombineArgs(profileArgs, $"{newArgs}\"{url}\""));
			var process = new ProcessStartInfo(Model.Command, args) { UseShellExecute = false };
			_ = Process.Start(process);
		}
		catch
		{
			// ignored
		}
		if (parent_view_model.OnShutdown != null)
		{
			parent_view_model.OnShutdown(this, EventArgs.Empty);
		}
		else
		{
			Application.Current?.Shutdown();
		}
		return;

		static string CombineArgs(string? args1, string args2)
		{
			if (string.IsNullOrEmpty(args1))
			{
				return args2;
			}
			return args1 + " " + args2;
		}
	}

	private readonly ApplicationViewModel parent_view_model;
}
