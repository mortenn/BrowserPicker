using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace BrowserPicker
{
	public class Browser
	{
		public string Name { get; set; }

		public string IconPath { get; set; }

		public string Command { get; set; }

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

		public ICommand Select => new DelegateCommand(Launch);

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
					return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(cmd))?.Length > 0;
				}
				catch
				{
					// Design time exceptionss
					return false;
				}
			}
		}

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
					Process.Start(Command, url);
			}
			catch
			{
				// ignored
			}
			Application.Current.Shutdown();
		}

		private BitmapFrame icon;
	}
}