using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using BrowserPicker.Lib;

namespace BrowserPicker.Configuration
{
	[DebuggerDisplay("{" + nameof(Model) + "." + nameof(BrowserModel.Name) + "}")]
	public class Browser : ViewModelBase<BrowserModel>
	{
		public Browser(BrowserModel model, ViewModel viewModel) : base(model)
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

				case nameof(BrowserModel.IconPath):
					icon = null;
					OnPropertyChanged(nameof(Thumbnail));
					break;
			}
		}

		public BitmapFrame Thumbnail
		{
			get
			{
				if (icon == null)
				{
					icon = GetBrowserIcon(Model.IconPath);
				}
				return icon;
			}
		}

		public DelegateCommand Select => new DelegateCommand(() => Launch(false), () => CanLaunch(false));
		public DelegateCommand SelectPrivacy => new DelegateCommand(() => Launch(true), () => CanLaunch(true));
		public DelegateCommand Disable => new DelegateCommand(() => Model.Disabled = !Model.Disabled);
		public DelegateCommand Remove => new DelegateCommand(() => Model.Removed = true);

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

		private static BitmapFrame GetBrowserIcon(string iconPath)
		{
			BitmapFrame _icon = null;
			if (!string.IsNullOrEmpty(iconPath))
			{
				var _iconPath = iconPath.Trim(new[] { '"', '\'', ' ', '\t', '\r', '\n' });
				try
				{
					if (!File.Exists(_iconPath) && _iconPath.Contains("%"))
						_iconPath = System.Environment.ExpandEnvironmentVariables(_iconPath);
					if (_iconPath.EndsWith(".exe"))
					{
						var iconData = Icon.ExtractAssociatedIcon(_iconPath)?.ToBitmap();
						if (iconData == null)
							return null;
						var stream = new MemoryStream();
						iconData.Save(stream, ImageFormat.Png);
						_icon = BitmapFrame.Create(stream);
					}
					else
						_icon = BitmapFrame.Create(File.Open(_iconPath, FileMode.Open, FileAccess.Read, FileShare.Read));
				}
				catch { }
			}
			return _icon;
		}

		private bool CanLaunch(bool privacy)
		{
			return !string.IsNullOrWhiteSpace(view_model.TargetURL) && !(privacy && Model.PrivacyArgs == null);
		}

		private void Launch(bool privacy)
		{
			//	throw new InvalidOperationException("This is a test, do not panic.");

			try
			{
				if (Config.Settings.UseAutomaticOrdering)
				{
					Config.Settings.UpdateCounter(this);
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

		private BitmapFrame icon;
		private readonly ViewModel view_model;
	}
}
