using BrowserPicker.Framework;
using System.Diagnostics;

namespace BrowserPicker
{
	[DebuggerDisplay("{" + nameof(Name) + "}")]
	public class BrowserModel : ModelBase
	{
		public BrowserModel() { }
		
		public BrowserModel(IWellKnownBrowser known, string icon, string shell)
		{
			name = known.Name;
			PrivacyArgs = known.PrivacyArgs;
			Executable = known.RealExecutable;
			IconPath = icon;
			Command = shell;
		}

		public BrowserModel(string name, string icon, string shell)
		{
			this.name = name;
			icon_path = icon;
			command = shell;
		}

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
				if (icon_path == value) return;
				icon_path = value;
				OnPropertyChanged();
			}
		}

		public string Command
		{
			get => command;
			set
			{
				if (command == value) return;
				command = value;
				OnPropertyChanged();
			}
		}

		public string Executable
		{
			get => executable;
			set
			{
				if (executable == value) return;
				executable = value;
				OnPropertyChanged();
			}
		}

		public string CommandArgs
		{
			get => command_args;
			set
			{
				if (command_args == value) return;
				command_args = value;
				OnPropertyChanged();
			}
		}

		public string PrivacyArgs
		{
			get => privacy_args;
			set
			{
				if (privacy_args == value) return;
				privacy_args = value;
				OnPropertyChanged();
			}
		}

		public int Usage { get; set; }

		public bool Disabled
		{
			get => disabled;
			set
			{
				disabled = value;
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
				OnPropertyChanged();
			}
		}

		private bool disabled;
		private bool removed;
		private string name;
		private string icon_path;
		private string command;
		private string executable;
		private string command_args;
		private string privacy_args;
	}
}
