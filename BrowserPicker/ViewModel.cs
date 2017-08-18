using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Microsoft.Win32;

namespace BrowserPicker
{
	public class ViewModel
	{
		public ViewModel()
		{
			Configuration = new Config();
			Choices = new ObservableCollection<Browser>(Configuration.BrowserList);
			if(Choices.Count == 0)
				FindBrowsers();
			if(Configuration.AlwaysPrompt)
				return;
			var active = Choices.Where(b => b.IsRunning).ToList();
			if (active.Count == 1)
				active[0].Select.Execute(null);
		}

		public ICommand RefreshBrowsers => new DelegateCommand(FindBrowsers);

		public Config Configuration { get; }

		public ObservableCollection<Browser> Choices { get; }

		public string TargetURL => Environment.GetCommandLineArgs()[1];

		private void FindBrowsers()
		{
			EnumerateBrowsers(@"SOFTWARE\Clients\StartMenuInternet");
			EnumerateBrowsers(@"SOFTWARE\WOW6432Node\Clients\StartMenuInternet");
			FindEdge();
			Configuration.BrowserList = Choices;
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
					}
				);
			}
		}

		private void EnumerateBrowsers(string subKey)
		{
			var root = Registry.LocalMachine.OpenSubKey(subKey);
			if (root == null)
				return;
			foreach (var browser in root.GetSubKeyNames().Where(n => n != "BrowserPicker"))
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
			if (icon?.Contains(",") ?? false)
				icon = icon.Split(',')[0];
			Choices.Add(
				new Browser
				{
					Name = name,
					IconPath = icon,
					Command = shell
				}
			);
		}
	}
}
