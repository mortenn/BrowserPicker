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

	public DelegateCommand Select => new(() => Launch(false), () => CanLaunch(false));
	public DelegateCommand SelectPrivacy => new(() => Launch(true), () => CanLaunch(true));
	public DelegateCommand Disable => new(() => Model.Disabled = !Model.Disabled);
	public DelegateCommand Remove => new(() => Model.Removed = true);
	public DelegateCommand Edit => new(() => OpenEditor(Model));

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
		return !string.IsNullOrWhiteSpace(parent_view_model.Url?.TargetURL) && !(privacy && Model.PrivacyArgs == null);
	}

	private void Launch(bool privacy)
	{
		if (parent_view_model.Url == null)
		{
			return;
		}
		try
		{
			if (App.Settings.UseAutomaticOrdering)
			{
				Model.Usage++;
			}

			if (parent_view_model.Configuration.AutoAddDefault)
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
			var args = CombineArgs(Model.CommandArgs, $"{newArgs}\"{parent_view_model.Url.TargetURL}\"");
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
