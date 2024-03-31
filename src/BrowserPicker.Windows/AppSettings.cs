using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrowserPicker.Framework;
using Microsoft.Win32;

namespace BrowserPicker.Windows
{
	public class AppSettings : ModelBase, IBrowserPickerConfiguration
	{
		public AppSettings()
		{
			BrowserList = GetBrowsers();
			Defaults = GetDefaults();
		}

		public bool AlwaysPrompt
		{
			get => Reg.Get<bool>();
			set { Reg.Set(value); OnPropertyChanged(); }
		}

		public bool DefaultsWhenRunning
		{
			get => Reg.Get<bool>();
			set { Reg.Set(value); OnPropertyChanged(); }
		}

		public int UrlLookupTimeoutMilliseconds
		{
			get => Reg.Get(2000);
			set { Reg.Set(value); OnPropertyChanged(); }
		}

		public bool UseAutomaticOrdering
		{
			get => Reg.Get(true);
			set { Reg.Set(value); OnPropertyChanged(); }
		}

		public bool DisableTransparency
		{
			get => Reg.Get(false);
			set { Reg.Set(value); OnPropertyChanged(); }
		}

		public bool DisableNetworkAccess
		{
			get => Reg.Get(false);
			set { Reg.Set(value); OnPropertyChanged(); }
		}

		public List<BrowserModel> BrowserList
		{
			get;
		}

		public void AddBrowser(BrowserModel browser)
		{
			var key = Reg.Open(nameof(BrowserList), browser.Name);
			key.Set(browser.Name);
			key.Set(browser.Command);
			key.Set(browser.Executable);
			key.Set(browser.CommandArgs);
			key.Set(browser.PrivacyArgs);
			key.Set(browser.IconPath);
			key.Set(browser.Usage);
			browser.PropertyChanged += BrowserConfiguration_PropertyChanged;

			BrowserList.Add(browser);
			OnPropertyChanged(nameof(BrowserList));
		}

		public List<DefaultSetting> Defaults
		{
			get;
		}

		public bool AlwaysUseDefault
		{
			get => Defaults.Any(d => d.Type == MatchType.Default);
			set
			{
				if (value == AlwaysUseDefault)
					return;

				if (value)
				{
					AddDefault(MatchType.Default, string.Empty, null);
					OnPropertyChanged(nameof(Defaults));
					OnPropertyChanged();
					return;
				}

				Defaults.RemoveAll(d => d.Type == MatchType.Default);
				OnPropertyChanged(nameof(Defaults));
				OnPropertyChanged();
			}
		}

		public string DefaultBrowser
		{
			get => Defaults.FirstOrDefault(d => d.Type == MatchType.Default)?.Browser;
			set
			{
				var selection = Defaults.FirstOrDefault(d => d.Type == MatchType.Default);
				if (selection != null)
					selection.Browser = value;
			}
		}

		public DefaultSetting AddDefault(MatchType matchType, string pattern, string browser)
		{
			var setting = GetDefaultSetting(null, browser);
			setting.Type = matchType;
			setting.Pattern = pattern;
			Defaults.Add(setting);
			OnPropertyChanged(nameof(Defaults));
			return setting;
		}

		public void FindBrowsers()
		{
			// Prefer 64 bit browsers to 32 bit ones, machine wide installs to user specific ones.
			EnumerateBrowsers(Registry.LocalMachine, @"SOFTWARE\Clients\StartMenuInternet");
			EnumerateBrowsers(Registry.CurrentUser, @"SOFTWARE\Clients\StartMenuInternet");
			EnumerateBrowsers(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Clients\StartMenuInternet");
			EnumerateBrowsers(Registry.CurrentUser, @"SOFTWARE\WOW6432Node\Clients\StartMenuInternet");

			if (!BrowserList.Any(browser => browser.Name.Contains("Edge")))
			{
				FindLegacyEdge();
			}
		}

		public async Task Start(CancellationToken cancellationToken)
		{
			await Task.Run(FindBrowsers, cancellationToken);
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
			if (targets.Length <= 0)
			{
				return;
			}
			var known = WellKnownBrowsers.Lookup("Edge", null);
			var appId = Path.GetFileName(targets[0]);
			var icon = Path.Combine(targets[0], "Assets", "MicrosoftEdgeSquare44x44.targetsize-32_altform-unplated.png");
			var shell = $"shell:AppsFolder\\{appId}!MicrosoftEdge";

			var model = new BrowserModel(known, icon, shell);
			AddOrUpdateBrowserModel(model);
		}

		private void EnumerateBrowsers(RegistryKey hive, string subKey)
		{
			var root = hive.OpenSubKey(subKey, false);
			if (root == null)
			{
				return;
			}
			foreach (var browser in root.GetSubKeyNames().Where(n => n != "BrowserPicker"))
			{
				var browserModel = GetBrowserDetails(root, browser);
				if (browserModel != null)
				{
					AddOrUpdateBrowserModel(browserModel);
				}
			}
		}

		private static BrowserModel GetBrowserDetails(RegistryKey root, string browser)
		{
			var reg = root.OpenSubKey(browser, false);
			if (reg == null)
			{
				return null;
			}

			var (name, icon, shell) = reg.GetBrowser();

			if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(shell))
			{
				return null;
			}

			var known = WellKnownBrowsers.Lookup(name, shell);
			return known != null
				? new BrowserModel(known, icon, shell)
				: new BrowserModel(name, icon, shell);
		}

		private void AddOrUpdateBrowserModel(BrowserModel model)
		{
			var update = BrowserList.FirstOrDefault(m => m.Name.Equals(model.Name, StringComparison.CurrentCultureIgnoreCase));
			if (update != null)
			{
				update.Command = model.Command;
				update.CommandArgs = model.CommandArgs;
				update.PrivacyArgs = model.PrivacyArgs;
				update.IconPath = model.IconPath;
				return;
			}
			AddBrowser(model);
		}

		private static List<DefaultSetting> GetDefaults()
		{
			var key = Reg.Open(nameof(Defaults));
			var values = key.GetValueNames();
			if (values.Contains("|Default|"))
			{
				var defaultBrowser = key.GetValue("|Default|");
				key.DeleteValue("|Default|");
				key.SetValue(string.Empty, defaultBrowser);
				values = key.GetValueNames();
			}
			return values.Select(name => GetDefaultSetting(name, (string)key.GetValue(name))).ToList();
		}

		private static DefaultSetting GetDefaultSetting(string key, string value)
		{
			var setting = DefaultSetting.Decode(key, value);
			setting.PropertyChanging += DefaultSetting_PropertyChanging;
			setting.PropertyChanged += DefaultSetting_PropertyChanged;
			return setting;
		}

		private static void DefaultSetting_PropertyChanging(object sender, PropertyChangingEventArgs e)
		{
			if (e.PropertyName != nameof(DefaultSetting.SettingKey))
			{
				return;
			}
			var model = (DefaultSetting)sender;
			if (model.Pattern == null)
			{
				return;
			}
			var key = Reg.Open(nameof(Defaults));
			var settingKey = model.SettingKey;
			if (model.IsValid && key.GetValue(settingKey) != null)
			{
				key.DeleteValue(settingKey);
			}
		}

		private static void DefaultSetting_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var key = Reg.Open(nameof(Defaults));
			var model = (DefaultSetting)sender;
			if (model.Pattern == null)
			{
				return;
			}
			switch (e.PropertyName)
			{
				case nameof(DefaultSetting.Deleted) when model.Deleted:
					var settingKey = model.SettingKey;
					if (model.IsValid && key.GetValue(settingKey) != null)
					{
						key.DeleteValue(settingKey);
					}
					model.PropertyChanging -= DefaultSetting_PropertyChanging;
					model.PropertyChanged -= DefaultSetting_PropertyChanged;
					break;
				
				case nameof(DefaultSetting.SettingKey) when model.IsValid:
				case nameof(DefaultSetting.SettingValue) when model.IsValid:
					key.SetValue(model.SettingKey, model.SettingValue ?? string.Empty, RegistryValueKind.String);
					break;
			}
		}

		private static List<BrowserModel> GetBrowsers()
		{
			var list = Reg.SubKey(nameof(BrowserList));
			if (list == null)
			{
				return [];
			}

			var browsers = list.GetSubKeyNames()
				.Select(browser => GetBrowser(list, browser))
				.Where(browser => browser != null)
				.OrderByDescending(b => b.Usage)
				.ToList();

			if (browsers.Any(browser => browser.Name.Equals("Microsoft Edge")))
			{
				var edge = browsers.FirstOrDefault(browser => browser.Name.Equals("Edge"));
				if (edge != null)
				{
					browsers.Remove(edge);
					list.DeleteSubKeyTree(edge.Name);
				}
			}

			list.Close();
			return browsers;
		}

		private static BrowserModel GetBrowser(RegistryKey list, string name)
		{
			var config = list.OpenSubKey(name, false);
			if (config == null) return null;
			var browser = new BrowserModel
			{
				Name = name,
				Command = config.Get<string>(null, nameof(BrowserModel.Command)),
				Executable = config.Get<string>(null, nameof(BrowserModel.Executable)),
				CommandArgs = config.Get<string>(null, nameof(BrowserModel.CommandArgs)),
				PrivacyArgs = config.Get<string>(null, nameof(BrowserModel.PrivacyArgs)),
				IconPath = config.Get<string>(null, nameof(BrowserModel.IconPath)),
				Usage = config.Get(0, nameof(BrowserModel.Usage)),
				Disabled = config.Get(false, nameof(BrowserModel.Disabled))
			};
			config.Close();
			if (browser.Command == null)
				return null;

			browser.PropertyChanged += BrowserConfiguration_PropertyChanged;
			return browser;
		}

		private static void BrowserConfiguration_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var model = (BrowserModel)sender;
			var config = Reg.SubKey(nameof(BrowserList), model.Name);
			switch (e.PropertyName)
			{
				case nameof(BrowserModel.Command): config.Set(model.Command, e.PropertyName); break;
				case nameof(BrowserModel.Executable): config.Set(model.Executable, e.PropertyName); break;
				case nameof(BrowserModel.CommandArgs): config.Set(model.CommandArgs, e.PropertyName); break;
				case nameof(BrowserModel.PrivacyArgs): config.Set(model.PrivacyArgs, e.PropertyName); break;
				case nameof(BrowserModel.IconPath): config.Set(model.IconPath, e.PropertyName); break;
				case nameof(BrowserModel.Usage): config.Set(model.Usage, e.PropertyName); break;
				case nameof(BrowserModel.Disabled): config.Set(model.Disabled, e.PropertyName); break;
				case nameof(BrowserModel.Removed):
					if (model.Removed)
					{
						model.PropertyChanged -= BrowserConfiguration_PropertyChanged;
						Reg.SubKey(nameof(BrowserList)).DeleteSubKey(model.Name);
					}
					break;
				default: return;
			}
		}

		private static readonly RegistryKey Reg = Registry.CurrentUser.Open("Software", nameof(BrowserPicker));
	}
}
