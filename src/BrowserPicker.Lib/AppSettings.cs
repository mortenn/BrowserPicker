using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.Win32;

namespace BrowserPicker.Lib
{
	public class AppSettings : ModelBase
	{
		private AppSettings()
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

		public DateTime LastBrowserScanTime
		{
			get => new DateTime(Reg.Get<long>());
			set { Reg.Set(value.Ticks); OnPropertyChanged(); }
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


		public List<BrowserModel> BrowserList
		{
			get;
		}

		public void AddBrowser(BrowserModel browser)
		{
			var list = Reg.CreateSubKey(nameof(BrowserList), true);

			var key = list.CreateSubKey(browser.Name, true);
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

		public void RemoveDefault(string fragment)
		{
			Reg.SubKey(nameof(Defaults))?.DeleteValue(fragment);
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
			var list = Reg.SubKey(nameof(BrowserList));
			if (list == null)
			{
				return new List<BrowserModel>();
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
				case nameof(BrowserModel.Command):     config.Set(model.Command,     e.PropertyName); break;
				case nameof(BrowserModel.Executable):  config.Set(model.Executable,  e.PropertyName); break;
				case nameof(BrowserModel.CommandArgs): config.Set(model.CommandArgs, e.PropertyName); break;
				case nameof(BrowserModel.PrivacyArgs): config.Set(model.PrivacyArgs, e.PropertyName); break;
				case nameof(BrowserModel.IconPath):    config.Set(model.IconPath,    e.PropertyName); break;
				case nameof(BrowserModel.Usage):       config.Set(model.Usage,       e.PropertyName); break;
				case nameof(BrowserModel.Disabled):    config.Set(model.Disabled,    e.PropertyName); break;
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

		private static readonly RegistryKey Reg = Registry.CurrentUser.CreateSubKey("Software\\BrowserPicker", true);

		public static AppSettings Settings = new AppSettings();
	}
}
