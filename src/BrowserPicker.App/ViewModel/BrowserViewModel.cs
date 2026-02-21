using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using BrowserPicker.Framework;
using BrowserPicker.View;
#if DEBUG
using JetBrains.Annotations;
#endif

namespace BrowserPicker.ViewModel;

/// <summary>
/// ViewModel class for representing and interacting with a browser in the application.
/// Encapsulates logic for managing browser configurations and commands.
/// </summary>
[DebuggerDisplay("{" + nameof(Model) + "." + nameof(BrowserModel.Name) + "}")]
public sealed class BrowserViewModel : ViewModelBase<BrowserModel>
{
#if DEBUG
	/// <summary>
	/// Parameterless constructor for WPF Designer support during debugging.
	/// Initializes the ViewModel with default values.
	/// </summary>
	[UsedImplicitly]
	public BrowserViewModel() : base(new BrowserModel())
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
		if (e.PropertyName == nameof(IApplicationSettings.UseAutomaticOrdering))
		{
			OnPropertyChanged(nameof(IsManuallyOrdered));
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
			case nameof(BrowserModel.PrivacyArgs):
				SelectPrivacy.RaiseCanExecuteChanged();
				break;
	
			case nameof(BrowserModel.Disabled):
				SelectPrivacy.RaiseCanExecuteChanged();
				Select.RaiseCanExecuteChanged();
				break;
		}
	}

	/// <summary>
	/// Gets a value indicating whether the browser list is ordered manually.
	/// </summary>
	public bool IsManuallyOrdered => !parent_view_model.Configuration.Settings.UseAutomaticOrdering;

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
	public DelegateCommand Remove => remove ??= new DelegateCommand(() => Model.Removed = true);
	
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
			ExpandFileUrls = Model.ExpandFileUrls
		};
		var editorVm = new BrowserViewModel(clone, parent_view_model);
		var editor = new BrowserEditor(editorVm);
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
		editor.Closing += OnDuplicateEditorClosing;
		editor.Show();
	}

	/// <summary>
	/// Opens the browser editor for modifying the given browser model.
	/// </summary>
	/// <param name="model">The browser model to be edited.</param>
	private void OpenEditor(BrowserModel model)
	{
		var temp = new BrowserModel
		{
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
			ExpandFileUrls = model.ExpandFileUrls
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
	/// Determines if the browser can be launched with the specified privacy setting.
	/// </summary>
	/// <param name="privacy">Whether the browser should be launched in privacy mode.</param>
	/// <returns>True if the browser can be launched; otherwise, false.</returns>
	private bool CanLaunch(bool privacy)
	{
		return !string.IsNullOrWhiteSpace(parent_view_model.Url.TargetURL) && !(privacy && Model.PrivacyArgs == null);
	}

	/// <summary>
	/// Launches the browser with the specified privacy mode setting by executing the associated command.
	/// </summary>
	/// <param name="privacy">Whether the browser should be launched in privacy mode.</param>
	private void Launch(bool privacy)
	{
		if (parent_view_model.Url.TargetURL == null)
		{
			return;
		}
		try
		{
			if (App.Settings.UseAutomaticOrdering)
			{
				Model.Usage++;
			}
	
			parent_view_model.Configuration.UrlOpened(parent_view_model.Url.HostName, Model.Id);
	
			var newArgs = privacy ? Model.PrivacyArgs : string.Empty;
			var url = parent_view_model.Url.GetTargetUrl(Model.ExpandFileUrls);
			var args = CombineArgs(Model.CommandArgs, $"{newArgs}\"{url}\"");
			var process = new ProcessStartInfo(Model.Command, args) { UseShellExecute = false };
			_ = Process.Start(process);
		}
		catch
		{
			// ignored
		}
		Application.Current?.Shutdown();
		return;
		
		string CombineArgs(string? args1, string args2)
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
