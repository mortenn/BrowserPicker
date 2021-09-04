using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
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
		public ViewModel()
		{
			UnderlyingTargetURL = "https://github.com/mortenn/BrowserPicker";
			force_choice = true;
			Configuration = Config.Settings;
			Choices = new ObservableCollection<Browser>(Configuration.BrowserList.Select(m => new Browser(m, this)));
		}

		public ViewModel(List<string> arguments)
		{
			var options = arguments.Where(arg => arg[0] == '/').ToList();
			TargetURL = arguments.Except(options).FirstOrDefault();
			UnderlyingTargetURL = TargetURL;
			force_choice = options.Contains("/choose");
			ConfigurationMode = TargetURL == null;
			Configuration = Config.Settings;
			Choices = new ObservableCollection<Browser>(Configuration.BrowserList.Select(m => new Browser(m, this)));
		}

		public void Initialize()
		{
			if (Choices.Count == 0 || (DateTime.Now - Configuration.LastBrowserScanTime) > TimeSpan.FromDays(7) || Choices.All(c => c.Model.PrivacyArgs == null))
			{
				FindBrowsers();
			}
			if (Configuration.AlwaysPrompt || ConfigurationMode || force_choice)
			{
				return;
			}
			if (TargetURL != null)
			{
				CheckDefaultBrowser();
			}
			var active = Choices.Where(b => b.IsRunning && !b.Model.Disabled).ToList();
			if (active.Count == 1)
			{
				active[0].Select.Execute(null);
			}
		}

		public async Task ScanURLAsync(CancellationToken token)
		{
			var url = new UrlHandler(TargetURL);
			try
			{
				await url.ScanURLAsync(token);
			}
			catch (TaskCanceledException)
			{
				// ignored
			}
			if (url.UnderlyingTargetURL != UnderlyingTargetURL)
			{
				UnderlyingTargetURL = url.UnderlyingTargetURL;
			}
		}

		private void CheckDefaultBrowser()
		{
			var defaults = Configuration.Defaults.ToList();
			if (defaults.Count <= 0)
				return;

			var url = new Uri(UnderlyingTargetURL);
			var auto = defaults
				.Select(rule => new { rule, matchLength = rule.MatchLength(url) })
				.Where(o => o.matchLength > 0)
				.ToList();
			if (auto.Count <= 0)
				return;

			var browser = auto.OrderByDescending(o => o.matchLength).First().rule.Browser;
			var start = Choices.FirstOrDefault(c => c.Model.Name == browser);
			if (start == null || Configuration.DefaultsWhenRunning && !start.IsRunning)
				return;

			start.Select.Execute(null);
		}


		public ICommand RefreshBrowsers => new DelegateCommand(FindBrowsers);

		public ICommand Configure => new DelegateCommand(() => ConfigurationMode = !ConfigurationMode);

		public ICommand Exit => new DelegateCommand(() => OnShutdown?.Invoke(this, EventArgs.Empty));

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

			if (!string.IsNullOrEmpty(browser.Model.Name) && !string.IsNullOrEmpty(browser.Model.Command))
			{
				Choices.Add(browser);
				Configuration.AddBrowser(browser.Model);
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

		public string TargetURL
		{
			get => targetURL;
			private set
			{
				if (Equals(targetURL, value))
				{
					return;
				}
				targetURL = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(IsShortenedURL));
			}
		}

		public string UnderlyingTargetURL
		{
			get => underlyingTargetURL;
			set
			{
				if (Equals(underlyingTargetURL, value))
				{
					return;
				}
				underlyingTargetURL = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(IsShortenedURL));
			}
		}

		public bool IsShortenedURL => TargetURL != UnderlyingTargetURL;

		public string EditURL
		{
			get => editURL;
			set
			{
				editURL = value;
				UnderlyingTargetURL = value;
			}
		}

		public bool Copied { get; set; }

		public EventHandler OnShutdown;

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
			var removed = Choices.Where(b => b.Model.Removed).ToList();
			if (removed.Count > 0)
				removed.ForEach(b => Choices.Remove(b));

			// Prefer 64 bit browsers to 32 bit ones, machine wide installs to user specific ones.
			EnumerateBrowsers(Registry.LocalMachine, @"SOFTWARE\Clients\StartMenuInternet");
			EnumerateBrowsers(Registry.CurrentUser, @"SOFTWARE\Clients\StartMenuInternet");
			EnumerateBrowsers(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Clients\StartMenuInternet");
			EnumerateBrowsers(Registry.CurrentUser, @"SOFTWARE\WOW6432Node\Clients\StartMenuInternet");

			if (!Choices.Any(browser => browser.Model.Name.Contains("Edge")))
			{
				FindLegacyEdge();
			}

			Configuration.LastBrowserScanTime = DateTime.Now;
		}

		/// <summary>
		/// This is used to detect the old Edge browser.
		/// If the computer has the new Microsoft Edge browser installed, this should never be called.
		/// </summary>
		private void FindLegacyEdge()
		{
			var systemApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SystemApps");
			if (!Directory.Exists(systemApps))
				return;

			var targets = Directory.GetDirectories(systemApps, "*MicrosoftEdge_*");
			if (targets.Length > 0)
			{
				var known = WellKnownBrowsers.Lookup("Edge", null);
				var appId = Path.GetFileName(targets[0]);
				var icon = Path.Combine(targets[0], "Assets", "MicrosoftEdgeSquare44x44.targetsize-32_altform-unplated.png");
				var shell = $"shell:AppsFolder\\{appId}!MicrosoftEdge";

				var model = new BrowserModel(known, icon, shell);
				AddOrUpdateBrowserModel(model);
			}
		}

		private void EnumerateBrowsers(RegistryKey hive, string subKey)
		{
			var root = hive.OpenSubKey(subKey, false);
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

			var icon = (string)reg.OpenSubKey("DefaultIcon", false)?.GetValue(null);
			if (icon?.Contains(",") ?? false)
				icon = icon.Split(',')[0];
			var shell = (string)reg.OpenSubKey("shell\\open\\command", false)?.GetValue(null);
			var known = WellKnownBrowsers.Lookup(name, shell);
			if (known != null)
			{
				var knownModel = new BrowserModel(known, icon, shell);
				AddOrUpdateBrowserModel(knownModel);
				return;
			}
			var model = new BrowserModel(name, icon, shell);
			AddOrUpdateBrowserModel(model);
		}

		private void AddOrUpdateBrowserModel(BrowserModel model)
		{
			var update = Configuration.BrowserList.FirstOrDefault(m => m.Name.Equals(model.Name, StringComparison.CurrentCultureIgnoreCase));
			if (update != null)
			{
				update.Command = model.Command;
				update.CommandArgs = model.CommandArgs;
				update.PrivacyArgs = model.PrivacyArgs;
				update.IconPath = model.IconPath;
				return;
			}
			Choices.Add(new Browser(model, this));
			Configuration.AddBrowser(model);
		}

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private bool configuration_mode;
		private string editURL;
		private string underlyingTargetURL;
		private string targetURL;
		private readonly bool force_choice;

		public void OnDeactivated()
		{
			if (!ConfigurationMode && !Debugger.IsAttached)
			{
				OnShutdown?.Invoke(this, EventArgs.Empty);
			}
		}
	}
}
