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

[DebuggerDisplay("{" + nameof(Model) + "." + nameof(BrowserModel.Name) + "}")]
public sealed class BrowserViewModel : ViewModelBase<BrowserModel>
{
#if DEBUG
	// WPF Designer
	[UsedImplicitly]
	public BrowserViewModel() : base(new BrowserModel())
	{
		parent_view_model = new ApplicationViewModel();
	}
#endif

	public BrowserViewModel(BrowserModel model, ApplicationViewModel viewModel) : base(model)
	{
		model.PropertyChanged += Model_PropertyChanged;
		parent_view_model = viewModel;
		parent_view_model.PropertyChanged += OnParentViewModelChanged;
		parent_view_model.Configuration.Settings.PropertyChanged += Settings_PropertyChanged;
	}

	private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(IApplicationSettings.UseAutomaticOrdering))
		{
			OnPropertyChanged(nameof(IsManuallyOrdered));
		}
	}

	private void OnParentViewModelChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(ApplicationViewModel.AltPressed))
		{
			OnPropertyChanged(nameof(AltPressed));
		}
	}

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

	public bool IsManuallyOrdered => !parent_view_model.Configuration.Settings.UseAutomaticOrdering;

	public DelegateCommand Select => select ??= new(() => Launch(false), () => CanLaunch(false));
	public DelegateCommand SelectPrivacy => selectPrivacy ??= new(() => Launch(true), () => CanLaunch(true));
	public DelegateCommand Disable => disable ??= new(() => Model.Disabled = !Model.Disabled);
	public DelegateCommand Remove => remove ??= new(() => Model.Removed = true);
	public DelegateCommand Edit => edit ??= new(() => OpenEditor(Model));
	public DelegateCommand MoveUp => moveUp ??= new DelegateCommand(() => Swap(1), () => CanSwap(1));
	public DelegateCommand MoveDown => moveDown ??= new DelegateCommand(() => Swap(-1), () => CanSwap(-1));

	private DelegateCommand? select;
	private DelegateCommand? selectPrivacy;
	private DelegateCommand? disable;
	private DelegateCommand? remove;
	private DelegateCommand? edit;
	private DelegateCommand? moveUp;
	private DelegateCommand? moveDown;

	private bool CanSwap(int offset)
	{
		var choices = parent_view_model.Choices.Where(vm => !vm.Model.Removed).ToList();
		var i = choices.IndexOf(this);
		var ni = i - offset;
		return ni >= 0 && ni < choices.Count;
	}

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

	private void OpenEditor(BrowserModel model)
	{
		var temp = new BrowserModel
		{
			Command = Model.Command,
			CommandArgs = model.CommandArgs,
			Executable = model.Executable,
			IconPath = model.IconPath,
			Name = model.Name,
			PrivacyArgs = model.PrivacyArgs
		};
		var editor = new BrowserEditor(new BrowserViewModel(temp, parent_view_model));
		editor.Show();
		editor.Closing += Editor_Closing;
	}

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
	}

	public string PrivacyTooltip
	{
		get
		{
			var known = WellKnownBrowsers.Lookup(Model.Name, null);
			return known?.PrivacyMode ?? "Open in privacy mode";
		}
	}

	public bool IsRunning
	{
		get
		{
			try
			{
				var session = Process.GetCurrentProcess().SessionId;

				if (Model.Command == "microsoft-edge:" || Model.Command.Contains("MicrosoftEdge"))
					return Process.GetProcessesByName("MicrosoftEdge").Any(p => p.SessionId == session);

				var cmd = Model.Command;
				if (cmd[0] == '"')
					cmd = cmd.Split('"')[1];

				var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Model.Executable ?? cmd));

				return processes.Any(p => p.SessionId == session && p.MainWindowHandle != 0);
			}
			catch
			{
				// Design time exceptions
				return false;
			}
		}
	}

	public bool AltPressed => parent_view_model.AltPressed;

	private bool CanLaunch(bool privacy)
	{
		return !string.IsNullOrWhiteSpace(parent_view_model.Url.TargetURL) && !(privacy && Model.PrivacyArgs == null);
	}

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

			if (parent_view_model.Configuration.AutoAddDefault && parent_view_model.Url.HostName != null)
			{
				try
				{
					parent_view_model.Configuration.NewDefaultBrowser = Model.Name;
					parent_view_model.Configuration.NewDefaultMatchType = MatchType.Hostname;
					parent_view_model.Configuration.NewDefaultPattern = parent_view_model.Url.HostName;
					parent_view_model.Configuration.AddDefault.Execute(null);
				}
				catch
				{
					// ignored
				}
			}

			var newArgs = privacy ? Model.PrivacyArgs : string.Empty;
			var url = parent_view_model.Url.UnderlyingTargetURL ?? parent_view_model.Url.TargetURL;
			var args = CombineArgs(Model.CommandArgs, $"{newArgs}\"{url}\"");
			var process = new ProcessStartInfo(Model.Command, args) { UseShellExecute = false };
			_ = Process.Start(process);
		}
		catch
		{
			// ignored
		}
		Application.Current?.Shutdown();
	}

	private static string CombineArgs(string? args1, string args2)
	{
		if (string.IsNullOrEmpty(args1))
		{
			return args2;
		}
		return args1 + " " + args2;
	}

	private readonly ApplicationViewModel parent_view_model;
}
