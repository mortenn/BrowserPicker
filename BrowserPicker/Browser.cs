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

		public DelegateCommand Select => new DelegateCommand(() => Launch(), () => IsUsable);
		public DelegateCommand SelectPrivacy => new DelegateCommand(() => Launch(true), () => PrivacyArgs != null && IsUsable);

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

		public bool IsUsable => Command != @"microsoft-edge:" || Environment.GetCommandLineArgs()[1].StartsWith("http");

		public int Usage { get; set; }

		private void Launch(bool privacy = false)
		{
			try
			{
				Config.UpdateCounter(this);
				var url = Environment.GetCommandLineArgs()[1];
				if (Command == MicrosoftEdge)
					Process.Start($"{MicrosoftEdge}{url}");
				else
				{
					var args = privacy ? PrivacyArgs : string.Empty;
					Process.Start(Command, $"{args} \"{url}\"");
				}
			}
			catch
			{
				// ignored
			}
			Application.Current.Shutdown();
		}

		private BitmapFrame icon;
		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private const string MicrosoftEdge = @"microsoft-edge:";
	}
}