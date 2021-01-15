using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;
using JetBrains.Annotations;

namespace BrowserPicker
{
	[DebuggerDisplay("{" + nameof(Name) + "}")]
	public class Browser : INotifyPropertyChanged
	{
		public string Name
		{
			get => name;
			set
			{
				name = value;
				OnPropertyChanged();
			}
		}

		public string IconPath
		{
			get => icon_path;
			set
			{
				icon_path = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(Thumbnail));
			}
		}

		public string Command
		{
			get => command;
			set
			{
				command = value;
				OnPropertyChanged();
			}
		}

		public string CommandArgs
		{
			get => commandArgs;
			set
			{
				commandArgs = value;
				OnPropertyChanged();
			}
		}

		public string PrivacyArgs
		{
			get
			{
				switch (Name)
				{
					default:
						return null;
					case "Mozilla Firefox":
						return "-private-window ";
					case "Internet Explorer":
						return "-private ";
					case "Google Chrome":
						return "--incognito ";
					case "Edge":
						return " -private";
				}
			}
		}

		public BitmapFrame Thumbnail
		{
			get
			{
				if (icon != null || string.IsNullOrEmpty(IconPath))
					return icon;
				if (IconPath.EndsWith("exe"))
				{
					var iconData = Icon.ExtractAssociatedIcon(IconPath)?.ToBitmap();
					if (iconData == null)
						return null;
					var stream = new MemoryStream();
					iconData.Save(stream, ImageFormat.Png);
					icon = BitmapFrame.Create(stream);
					return icon;
				}
				icon = BitmapFrame.Create(File.Open(IconPath, FileMode.Open, FileAccess.Read, FileShare.Read));
				return icon;
			}
		}

		public DelegateCommand Select => new DelegateCommand(() => Launch(false), () => CanLaunch(false));
		public DelegateCommand SelectPrivacy => new DelegateCommand(() => Launch(true), () => CanLaunch(true));
		public DelegateCommand Disable => new DelegateCommand(() => Disabled = !Disabled);
		public DelegateCommand Remove => new DelegateCommand(() => Removed = true);

		public bool IsRunning
		{
			get
			{
				try
				{
					var session = Process.GetCurrentProcess().SessionId;

					if (Command == "microsoft-edge:")
						return Process.GetProcessesByName("MicrosoftEdge").Any(p => p.SessionId == session);

					var cmd = Command;
					if (cmd[0] == '"')
						cmd = cmd.Split('"')[1];
					return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(cmd)).Any(p => p.SessionId == session);
				}
				catch
				{
					// Design time exceptions
					return false;
				}
			}
		}

		public bool IsUsable => App.TargetURL != null;

		public int Usage { get; set; }

		public bool Disabled
		{
			get => disabled;
			set
			{
				disabled = value;
				Config.UpdateBrowserDisabled(this);
				OnPropertyChanged();
			}
		}

		public bool Removed
		{
			get => removed;
			set
			{
				removed = value;
				Disabled = value;
				Config.RemoveBrowser(this);
				OnPropertyChanged();
			}
		}

		private bool CanLaunch(bool privacy)
		{
			return IsUsable && !(privacy && PrivacyArgs == null);
		}

		private void Launch(bool privacy)
		{
			try
			{
				Config.UpdateCounter(this);
				var args = CommandArgs;
				if (Name == "Edge")
				{
					var newArgs = (privacy ? "-private " : string.Empty) + App.TargetURL;
					args = CombineArgs(args, newArgs);
				}
				else
				{
					var newArgs = privacy ? PrivacyArgs : string.Empty;
					args = CombineArgs(args, $"{newArgs}\"{App.TargetURL}\"");
				}
				Process.Start(Command, args);
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
		private bool disabled;
		private bool removed;
		private string name;
		private string icon_path;
		private string command;
		private string commandArgs;
		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}