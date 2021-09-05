using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using JetBrains.Annotations;

namespace BrowserPicker.ViewModel
{
	[DebuggerDisplay("{" + nameof(Model) + "." + nameof(BrowserModel.Name) + "}")]
	public class BrowserViewModel : ViewModelBase<BrowserModel>
	{
		// WPF Designer
		[UsedImplicitly]
		public BrowserViewModel() : base(new BrowserModel()) { }

		public BrowserViewModel(BrowserModel model, ApplicationViewModel viewModel) : base(model)
		{
			model.PropertyChanged += Model_PropertyChanged;
			view_model = viewModel;
		}

		private void Model_PropertyChanged(object sender, PropertyChangedEventArgs e)
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

		public DelegateCommand Select => new DelegateCommand(() => Launch(false), () => CanLaunch(false));
		public DelegateCommand SelectPrivacy => new DelegateCommand(() => Launch(true), () => CanLaunch(true));
		public DelegateCommand Disable => new DelegateCommand(() => Model.Disabled = !Model.Disabled);
		public DelegateCommand Remove => new DelegateCommand(() => Model.Removed = true);

		public string PrivacyTooltip
		{
			get
			{
				var known = WellKnownBrowsers.Lookup(Model.Name, null);
				return known.PrivacyMode ?? "Open in privacy mode";
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

					return processes.Any(p => p.SessionId == session);
				}
				catch
				{
					// Design time exceptions
					return false;
				}
			}
		}

		private bool CanLaunch(bool privacy)
		{
			return !string.IsNullOrWhiteSpace(view_model.TargetURL) && !(privacy && Model.PrivacyArgs == null);
		}

		private void Launch(bool privacy)
		{
			try
			{
				if (AppSettings.Settings.UseAutomaticOrdering)
				{
					Model.Usage++;
				}

				var args = Model.CommandArgs;
				var newArgs = privacy ? Model.PrivacyArgs : string.Empty;
				args = CombineArgs(Model.CommandArgs, $"{newArgs}\"{view_model.TargetURL}\"");
				_ = Process.Start(Model.Command, args);
			}
			catch
			{
				// ignored
			}
			Application.Current?.Shutdown();
		}

		private static string CombineArgs(string args1, string args2)
		{
			if (string.IsNullOrEmpty(args1))
			{
				return args2;
			}
			return args1 + " " + args2;
		}

		private readonly ApplicationViewModel view_model;
	}
}
