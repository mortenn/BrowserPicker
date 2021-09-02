using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;
using BrowserPicker.Lib;
using JetBrains.Annotations;

namespace BrowserPicker.Configuration
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
			get => command_args;
			set
			{
				command_args = value;
				OnPropertyChanged();
			}
		}

		public string PrivacyArgs
		{
			get
			{
				string arg = null;
				switch (Name)
				{
					default:
						arg = null;
						break;
					case "Mozilla Firefox":
						arg = FIREFOX_PRIVATE_ARG;
						break;
					case "Internet Explorer":
						arg = IE_PRIVATE_ARG;
						break;
					case "Google Chrome":
						arg = CHROME_PRIVATE_ARG;
						break;
					case "Microsoft Edge":
					case "Edge":
						arg = EDGE_PRIVATE_ARG;
						break;
				}
				if (string.IsNullOrEmpty(arg) && !string.IsNullOrEmpty(Command))
				{
					if (Command.IndexOf("chrome.exe", System.StringComparison.CurrentCultureIgnoreCase) != -1)
						arg = CHROME_PRIVATE_ARG;
					else if (Command.IndexOf("msedge.exe", System.StringComparison.CurrentCultureIgnoreCase) != -1)
						arg = EDGE_PRIVATE_ARG;
					else if (Command.IndexOf("iexplore.exe", System.StringComparison.CurrentCultureIgnoreCase) != -1)
						arg = IE_PRIVATE_ARG;
					else if (Command.IndexOf("firefox.exe", System.StringComparison.CurrentCultureIgnoreCase) != -1)
						arg = FIREFOX_PRIVATE_ARG;
				}
				return arg;
			}
		}

		public BitmapFrame Thumbnail
		{
			get
			{
				if (icon == null)
				{
					icon = GetBrowserIcon(IconPath);
				}
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

					if (Command == "microsoft-edge:" || Command.Contains("MicrosoftEdge"))
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
				Config.Settings.UpdateBrowserDisabled(this);
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
				Config.Settings.RemoveBrowser(this);
				OnPropertyChanged();
			}
		}

		private BitmapFrame GetBrowserIcon(string iconPath)
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
			if (_icon == null)
				_icon = GetDefaultIcon();
			return _icon;
		}

		private BitmapFrame GetDefaultIcon()
		{
			BitmapFrame _icon = null;
			try
			{
				_icon = BitmapFrame.Create(new System.Uri("pack://application:,,,/Resources/web_icon.png"));
			}
			catch { }
			return _icon;
		}

		private bool CanLaunch(bool privacy)
		{
			return IsUsable && !(privacy && PrivacyArgs == null);
		}

		private void Launch(bool privacy)
		{
			try
			{
				if (Config.Settings.UseAutomaticOrdering)
					Config.Settings.UpdateCounter(this);
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
		private string command_args;
		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private const string IE_PRIVATE_ARG = "-private ";
		private const string FIREFOX_PRIVATE_ARG = "-private-window ";
		private const string CHROME_PRIVATE_ARG = "--incognito ";
		private const string EDGE_PRIVATE_ARG = "-inprivate ";
	}
}
