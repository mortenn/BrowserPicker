using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using BrowserPicker.Configuration;
using BrowserPicker.Lib;
using BrowserPicker.View;
using JetBrains.Annotations;
using Microsoft.Win32;

namespace BrowserPicker
{
	public class ViewModel : INotifyPropertyChanged
	{
		// Used by WPF designer
		[UsedImplicitly]
		public ViewModel() : this(true)
		{
		}

		public ViewModel(bool forceChoice)
		{
			ConfigurationMode = App.TargetURL == null;
			Configuration = Config.Settings;

			Choices = new ObservableCollection<Browser>(Configuration.BrowserList);
			force_choice = forceChoice;
		}

		public void Initialize()
		{
			if (Choices.Count == 0 || (DateTime.Now - Configuration.LastBrowserScanTime) > TimeSpan.FromDays(7))
				FindBrowsers();

			if (Configuration.AlwaysPrompt || ConfigurationMode || force_choice)
				return;

			if (App.TargetURL != null)
				CheckDefaultBrowser();

			var active = Choices.Where(b => b.IsRunning && !b.Disabled).ToList();
			if (active.Count == 1)
				active[0].Select.Execute(null);
		}

		private void CheckDefaultBrowser()
		{
			var defaults = Configuration.Defaults.ToList();
			if (defaults.Count <= 0)
				return;

			var url = new Uri(App.UnderlyingTargetURL);
			var auto = defaults
				.Select(rule => new { rule, matchLength = rule.MatchLength(url) })
				.Where(o => o.matchLength > 0)
				.ToList();
			if (auto.Count <= 0)
				return;

			var browser = auto.OrderByDescending(o => o.matchLength).First().rule.Browser;
			var start = Choices.FirstOrDefault(c => c.Name == browser);
			if (start == null || Configuration.DefaultsWhenRunning && !start.IsRunning)
				return;

			start.Select.Execute(null);
		}


		public ICommand RefreshBrowsers => new DelegateCommand(FindBrowsers);

		public ICommand Configure => new DelegateCommand(() => ConfigurationMode = !ConfigurationMode);

		public ICommand Exit => new DelegateCommand(() => Application.Current.Shutdown());

		public ICommand CopyUrl => new DelegateCommand(PerformCopyUrl);

		public ICommand Edit => new DelegateCommand(OpenURLEditor);


		public DelegateCommand AddBrowser => new DelegateCommand(AddBrowserManually);

		private void AddBrowserManually()
		{
			var editor = new BrowserEditor();
			editor.Show();
			editor.Closing += Editor_Closing;
		}

		private void Editor_Closing(object sender, CancelEventArgs e)
		{
			((Window)sender).Closing -= Editor_Closing;
			if (!(((Window)sender).DataContext is Browser browser))
				return;

			if (!string.IsNullOrEmpty(browser.Name) && !string.IsNullOrEmpty(browser.Command))
			{
				Choices.Add(browser);
				Configuration.BrowserList = Choices;
			}
		}

		public Config Configuration { get; }

		public ObservableCollection<Browser> Choices { get; }

		public bool ConfigurationMode
		{
			get => configuration_mode;
			set
			{
				configuration_mode = value;
				OnPropertyChanged();
			}
		}

		public string TargetURL => App.TargetURL;
		public string UnderlyingTargetURL => App.UnderlyingTargetURL;

		public string EditURL
		{
			get => editURL;
			set
			{
				editURL = value;
				App.OverrideURL(value);
			}
		}

		public bool Copied { get; set; }

		private void PerformCopyUrl()
		{
			try
			{
				var thread = new Thread(() => Clipboard.SetText(UnderlyingTargetURL));
				thread.SetApartmentState(ApartmentState.STA);
				thread.Start();
				thread.Join();
				Copied = true;
				OnPropertyChanged(nameof(Copied));
			}
			catch
			{
				// ignored
			}
		}

		private void OpenURLEditor()
		{
			EditURL = UnderlyingTargetURL;
			OnPropertyChanged(nameof(EditURL));
		}

		private void FindBrowsers()
		{
			var removed = Choices.Where(b => b.Removed).ToList();
			if (removed.Count > 0)
				removed.ForEach(b => Choices.Remove(b));

			EnumerateBrowsers(@"SOFTWARE\Clients\StartMenuInternet");
			EnumerateBrowsers(@"SOFTWARE\WOW6432Node\Clients\StartMenuInternet");
			if (!Choices.Any(browser => browser.Name.Contains("Edge")))
				FindEdge();
			Configuration.BrowserList = Choices;
			Configuration.LastBrowserScanTime = DateTime.Now;
		}

		private void FindEdge()
		{
			if (Choices.Any(b => b.Name.Equals("Edge")))
				return;

			var systemApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SystemApps");
			if (!Directory.Exists(systemApps))
				return;

			var targets = Directory.GetDirectories(systemApps, "*MicrosoftEdge_*");
			if (targets.Length > 0)
			{
				Choices.Add(
					new Browser
					{
						Name = "Edge",
						Command = $"shell:AppsFolder\\{Path.GetFileName(targets[0])}!MicrosoftEdge",
						IconPath = Path.Combine(targets[0], "Assets", "MicrosoftEdgeSquare44x44.targetsize-32_altform-unplated.png")
					}
				);
			}
		}

		private void EnumerateBrowsers(string subKey)
		{
			var root = Registry.LocalMachine.OpenSubKey(subKey, false);
			if (root == null)
				return;
			foreach (var browser in root.GetSubKeyNames().Where(n => n != "BrowserPicker"))
				GetBrowserDetails(root, browser);
		}

		private void GetBrowserDetails(RegistryKey root, string browser)
		{
			var reg = root.OpenSubKey(browser, false);
			if (reg == null)
				return;

			var name = (string)reg.GetValue(null);
			if (Choices.Any(c => c.Name == name) || string.IsNullOrWhiteSpace(name))
				return;

			var icon = (string)reg.OpenSubKey("DefaultIcon", false)?.GetValue(null);
			var shell = (string)reg.OpenSubKey("shell\\open\\command", false)?.GetValue(null);
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

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private bool configuration_mode;
		private string editURL;
		private readonly bool force_choice;

		public void OnDeactivated()
		{
			if (!ConfigurationMode)
				Application.Current.Shutdown();
		}
	}
}
