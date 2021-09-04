using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using BrowserPicker.Lib;
using Microsoft.Win32;

namespace BrowserPicker.Configuration
{
	public class Config : ModelBase
	{
		private Config()
		{
			BrowserList = GetBrowsers();
			Defaults = GetDefaults();
		}

		public bool AlwaysPrompt
		{
			get => Reg.Get<bool>(nameof(AlwaysPrompt));
			set
			{
				Reg.Set(nameof(AlwaysPrompt), value);
				OnPropertyChanged();
			}
		}

		public bool DefaultsWhenRunning
		{
			get => Reg.Get<bool>(nameof(DefaultsWhenRunning));
			set
			{
				Reg.Set(nameof(DefaultsWhenRunning), value);
				OnPropertyChanged();
			}
		}

		public int UrlLookupTimeoutMilliseconds
		{
			get => Reg.Get(nameof(UrlLookupTimeoutMilliseconds), 2000);
			set
			{
				Reg.Set(nameof(UrlLookupTimeoutMilliseconds), value);
				OnPropertyChanged();
			}
		}

		public DateTime LastBrowserScanTime
		{
			get => new DateTime(Reg.Get<long>(nameof(LastBrowserScanTime)));
			set
			{
				Reg.Set(nameof(LastBrowserScanTime), value.Ticks);
				OnPropertyChanged();
			}
		}

		public bool UseAutomaticOrdering
		{
			get => Reg.Get(nameof(UseAutomaticOrdering), true);
			set
			{
				Reg.Set(nameof(UserPreferenceCategory), value);
				OnPropertyChanged();
			}
		}

		public bool DisableTransparency
		{
			get => Reg.Get(nameof(DisableTransparency), false);
			set
			{
				Reg.Set(nameof(DisableTransparency), value);
				OnPropertyChanged();
			}
		}


		public List<BrowserModel> BrowserList
		{
			get;
		}

		public void RemoveBrowser(Browser browser)
		{
			if (BrowserList.Contains(browser.Model))
			{
				BrowserList.Remove(browser.Model);
				OnPropertyChanged(nameof(BrowserList));
			}
			Reg.DeleteSubKeyTree(Path.Combine(nameof(BrowserList), browser.Model.Name), false);
		}

		public void AddBrowser(BrowserModel browser)
		{
			var list = Reg.CreateSubKey(nameof(BrowserList), true);

			var key = list.CreateSubKey(browser.Name, true);
			key.Set(nameof(BrowserModel.Name), browser.Name);
			key.Set(nameof(BrowserModel.Command), browser.Command);
			key.Set(nameof(BrowserModel.Executable), browser.Executable);
			key.Set(nameof(BrowserModel.CommandArgs), browser.CommandArgs);
			key.Set(nameof(BrowserModel.PrivacyArgs), browser.PrivacyArgs);
			key.Set(nameof(BrowserModel.IconPath), browser.IconPath);
			key.Set(nameof(BrowserModel.Usage), browser.Usage);
			browser.PropertyChanged += BrowserConfiguration_PropertyChanged;

			BrowserList.Add(browser);
			OnPropertyChanged(nameof(BrowserList));
		}


		public List<DefaultSetting> Defaults
		{
			get;
		}

		public void RemoveDefault(string fragment)
		{
			Reg.OpenSubKey(nameof(Defaults), true)?.DeleteValue(fragment);
		}

		public DefaultSetting AddDefault(string fragment, string browser)
		{
			var setting = GetDefaultSetting(null, browser);
			setting.Fragment = fragment;
			Defaults.Add(setting);
			OnPropertyChanged(nameof(Defaults));
			return setting;
		}


		private static List<DefaultSetting> GetDefaults()
		{
			var key = Reg.CreateSubKey(nameof(Defaults), true);
			var values = key.GetValueNames();
			return values.Select(name => GetDefaultSetting(name, (string)key.GetValue(name))).ToList();
		}

		private static DefaultSetting GetDefaultSetting(string fragment, string browser)
		{
			var setting = new DefaultSetting(fragment, browser);
			setting.PropertyChanged += DefaultSetting_PropertyChanged;
			return setting;
		}

		private static void DefaultSetting_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var key = Reg.CreateSubKey(nameof(Defaults), true);
			var model = (DefaultSetting)sender;
			switch (e.PropertyName)
			{
				case nameof(DefaultSetting.IsValid):
					if (model.IsValid)
					{
						key.DeleteValue(model.Fragment);
					}
					break;

				case nameof(DefaultSetting.Fragment):
				case nameof(DefaultSetting.Browser):
					if (model.IsValid)
					{
						key.SetValue(model.Fragment, model.Browser ?? string.Empty, RegistryValueKind.String);
					}
					break;
			}
		}

		private static List<BrowserModel> GetBrowsers()
		{

			var list = Reg.OpenSubKey("BrowserList", true);
			if (list == null)
				return new List<BrowserModel>();

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
				Command = config.Get<string>(nameof(BrowserModel.Command)),
				Executable = config.Get<string>(nameof(BrowserModel.Executable)),
				CommandArgs = config.Get<string>(nameof(BrowserModel.CommandArgs)),
				PrivacyArgs = config.Get<string>(nameof(BrowserModel.PrivacyArgs)),
				IconPath = config.Get<string>(nameof(BrowserModel.IconPath)),
				Usage = config.Get<int>(nameof(BrowserModel.Usage)),
				Disabled = config.Get<bool>(nameof(BrowserModel.Disabled))
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
			var key = Path.Combine(nameof(BrowserList), model.Name);
			object value;
			var kind = RegistryValueKind.String;
			switch (e.PropertyName)
			{
				case nameof(BrowserModel.Command):
					value = model.Command;
					break;

				case nameof(BrowserModel.Executable):
					value = model.Executable;
					break;

				case nameof(BrowserModel.CommandArgs):
					value = model.CommandArgs;
					break;

				case nameof(BrowserModel.PrivacyArgs):
					value = model.PrivacyArgs;
					break;

				case nameof(BrowserModel.IconPath):
					value = model.IconPath;
					break;

				case nameof(BrowserModel.Usage):
					kind = RegistryValueKind.DWord;
					value = model.Usage;
					break;

				case nameof(BrowserModel.Disabled):
					kind = RegistryValueKind.DWord;
					value = model.Disabled ? 1 : 0;
					break;

				default:
					return;
			}

			Reg.OpenSubKey(key, true)?.SetValue(e.PropertyName, value, kind);
		}

		private static readonly RegistryKey Reg = Registry.CurrentUser.CreateSubKey("Software\\BrowserPicker", true);

		public static Config Settings = new Config();
	}
}
