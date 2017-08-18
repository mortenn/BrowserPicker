using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;
using BrowserPicker.Annotations;

namespace BrowserPicker
{
	public class Browser : INotifyPropertyChanged
	{
		public string Name { get; set; }

		public string IconPath { get; set; }

		public string Command { get; set; }

		public bool PrivacyMode
		{
			get => privacy_mode;
			set
			{
				privacy_mode = value;
				Select.RaiseCanExecuteChanged();
				OnPropertyChanged();
				OnPropertyChanged(nameof(IsUsable));
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
						return "-private-window";
					case "Internet Explorer":
						return "-private";
					case "Google Chrome":
						return "--incognito";
				}
			}
		}

		public BitmapFrame Thumbnail
		{
			get
			{
				if (icon != null)
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

		public DelegateCommand Select => new DelegateCommand(Launch, () => IsUsable);

		public bool IsRunning
		{
			get
			{
				try
				{
					if (Command == "microsoft-edge:")
						return Process.GetProcessesByName("MicrosoftEdge").Length > 0;

					var cmd = Command;
					if (cmd[0] == '"')
						cmd = cmd.Split('"')[1];
					return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(cmd)).Length > 0;
				}
				catch
				{
					// Design time exceptionss
					return false;
				}
			}
		}

		public bool IsUsable => !privacy_mode || PrivacyArgs != null;

		public int Usage { get; set; }

		private void Launch()
		{
			try
			{
				Config.UpdateCounter(this);
				var url = Environment.GetCommandLineArgs()[1];
				if (Command == @"microsoft-edge:")
					Process.Start("microsoft-edge:" + url);
				else
				{
					var args = privacy_mode ? PrivacyArgs : string.Empty;
					Process.Start(Command, args + " " + url);
				}
			}
			catch
			{
				return;
				// ignored
			}
			Application.Current.Shutdown();
		}

		private BitmapFrame icon;
		private bool privacy_mode;
		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}