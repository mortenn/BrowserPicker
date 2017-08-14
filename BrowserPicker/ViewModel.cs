using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace BrowserPicker
{
	public class ViewModel
	{
		public ViewModel() : this(true) { }

		public ViewModel(bool designer)
		{
			isDesignTime = designer;
			Choices = new ObservableCollection<Browser>();
			FindBrowsers();
			FindEdge();
			if (isDesignTime)
				return;
			var active = Choices.Where(b => b.IsRunning).ToList();
			if (active.Count == 1)
				active[0].Select.Execute(null);
		}

		public ObservableCollection<Browser> Choices { get; }

		public string TargetURL
		{
			get { return isDesignTime ? "https://google.com/long-url-sample-for-designer-purposes-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" : Environment.GetCommandLineArgs()[1]; }
		}

		private void FindBrowsers()
		{
			EnumerateBrowsers(@"SOFTWARE\Clients\StartMenuInternet");
			EnumerateBrowsers(@"SOFTWARE\WOW6432Node\Clients\StartMenuInternet");
		}

		private void FindEdge()
		{
			var systemApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SystemApps");
			if (!Directory.Exists(systemApps))
				return;
			var targets = Directory.GetDirectories(systemApps, "*MicrosoftEdge*");
			if (targets.Length > 0)
			{
				Choices.Add(
					new Browser
					{
						Name = "Edge",
						Command = "microsoft-edge:",
						IconPath = Path.Combine(targets[0], "Assets", "MicrosoftEdgeSquare44x44.targetsize-32_altform-unplated.png"),
						IsRunning = isDesignTime || Process.GetProcessesByName("MicrosoftEdge").Length > 0
					}
				);
			}
		}

		private void EnumerateBrowsers(string subKey)
		{
			var root = Registry.LocalMachine.OpenSubKey(subKey);
			if (root == null)
				return;
			foreach (var browser in root.GetSubKeyNames())
				GetBrowserDetails(root, browser);
		}

		private void GetBrowserDetails(RegistryKey root, string browser)
		{
			var reg = root.OpenSubKey(browser);
			if (reg == null)
				return;

			var name = (string)reg.GetValue(null);
			if (Choices.Any(c => c.Name == name))
				return;

			var icon = (string)reg.OpenSubKey("DefaultIcon")?.GetValue(null);
			var shell = (string)reg.OpenSubKey("shell\\open\\command")?.GetValue(null);
			var cmd = shell;
			if (shell[0] == '"')
				cmd = shell.Split('"')[1];
			var running = isDesignTime || Process.GetProcessesByName(Path.GetFileNameWithoutExtension(cmd))?.Length > 0;
			if (icon?.Contains(",") ?? false)
				icon = icon.Split(',')[0];
			Choices.Add(
				new Browser
				{
					Name = name,
					IconPath = icon,
					Command = shell,
					IsRunning = running
				}
			);
		}

		private bool isDesignTime;
	}
}
