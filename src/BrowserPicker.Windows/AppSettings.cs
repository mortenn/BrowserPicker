using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrowserPicker.Framework;
using Microsoft.Win32;

namespace BrowserPicker.Windows;

public sealed class AppSettings : ModelBase, IBrowserPickerConfiguration
{
	public AppSettings()
	{
		BrowserList = GetBrowsers();
		Defaults = GetDefaults();
		use_fallback_default = !string.IsNullOrWhiteSpace(Defaults.FirstOrDefault(d => d.Type == MatchType.Default)?.Browser);
	}

	public bool AlwaysPrompt
	{
		get => Reg.Get<bool>();
		set { Reg.Set(value); OnPropertyChanged(); }
	}

	public bool AlwaysUseDefaults
	{
		get => Reg.Get<bool>();
		set { Reg.Set(value); OnPropertyChanged(); }
	}

	public bool AlwaysAskWithoutDefault
	{
		get => Reg.Get<bool>();
		set
		{
			Reg.Set(value);
			OnPropertyChanged();
			if (value && use_fallback_default)
			{
				UseFallbackDefault = false;
			}
		}
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
		key.Set(browser.Name, nameof(browser.Name));
		key.Set(browser.Command, nameof(browser.Command));
		key.Set(browser.Executable, nameof(browser.Executable));
		key.Set(browser.CommandArgs, nameof(browser.CommandArgs));
		key.Set(browser.PrivacyArgs, nameof(browser.PrivacyArgs));
		key.Set(browser.IconPath, nameof(browser.IconPath));
		key.Set(browser.Usage, nameof(browser.Usage));
		browser.PropertyChanged += BrowserConfiguration_PropertyChanged;

		BrowserList.Add(browser);
		OnPropertyChanged(nameof(BrowserList));
	}

	public List<DefaultSetting> Defaults
	{
		get;
	}

	private bool use_fallback_default;

	public bool UseFallbackDefault
	{
		get => use_fallback_default;
		set
		{
			if (value == UseFallbackDefault)
				return;

			if (value)
			{
				AlwaysAskWithoutDefault = false;
				use_fallback_default = true;
				if (Defaults.All(d => d.Type != MatchType.Default))
				{
					AddDefault(MatchType.Default, string.Empty, null);
				}
				OnPropertyChanged();
				return;
			}

			use_fallback_default = false;
			OnPropertyChanged();
			DefaultBrowser = null;
		}
	}

	public string? DefaultBrowser
	{
		get => Defaults.FirstOrDefault(d => d.Type == MatchType.Default)?.Browser;
		set
		{
			var selection = Defaults.FirstOrDefault(d => d.Type == MatchType.Default);
			if (selection == null && !string.IsNullOrWhiteSpace(value))
			{
				selection = GetDefaultSetting(string.Empty, string.Empty)!;
				selection.Type = MatchType.Default;
				Defaults.Add(selection);
			}
			if (selection != null && value != selection.Browser)
			{
				selection.Browser = value;
				use_fallback_default = value != null;
				OnPropertyChanged(nameof(UseFallbackDefault));
			}
			OnPropertyChanged();
		}
	}

	public DefaultSetting? AddDefault(MatchType matchType, string pattern, string? browser)
	{
		var setting = GetDefaultSetting(null, browser);
		if (setting == null)
		{
			return null;
		}
		setting.Type = matchType;
		setting.Pattern = pattern;
		Defaults.Add(setting);
		OnPropertyChanged(nameof(Defaults));
		return setting;
	}

	public void FindBrowsers()
	{
		// Prefer 64 bit browsers to 32 bit ones, machine wide installations to user specific ones.
		EnumerateBrowsers(Registry.LocalMachine, @"SOFTWARE\Clients\StartMenuInternet");
		EnumerateBrowsers(Registry.CurrentUser, @"SOFTWARE\Clients\StartMenuInternet");
		EnumerateBrowsers(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Clients\StartMenuInternet");
		EnumerateBrowsers(Registry.CurrentUser, @"SOFTWARE\WOW6432Node\Clients\StartMenuInternet");

		if (!BrowserList.Any(browser => browser.Name.Contains("Edge")))
		{
			FindLegacyEdge();
		}
	}

	public Task Start(CancellationToken cancellationToken)
	{
		return Task.Run(FindBrowsers, cancellationToken);
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
		if (known == null)
		{
			return;
		}
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

	private static BrowserModel? GetBrowserDetails(RegistryKey root, string browser)
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
			values = ConvertLegacyDefault(key);
		}

		return
		[
			..
			from pattern in values
			where pattern is not null
			let browser = key.GetValue(pattern) as string
			where browser is not null
			let setting = GetDefaultSetting(pattern, browser)
			where setting is not null
			select setting
		];
	}

	private static string[] ConvertLegacyDefault(RegistryKey key)
	{
		var defaultBrowser = key.GetValue("|Default|");
		key.DeleteValue("|Default|");
		if (defaultBrowser != null)
		{
			key.SetValue(string.Empty, defaultBrowser);
		}
		return key.GetValueNames();
	}

	private static DefaultSetting? GetDefaultSetting(string? key, string? value)
	{
		if (value == null)
		{
			return null;
		}
		var setting = DefaultSetting.Decode(key, value);
		if (setting == null)
		{
			return null;
		}
		setting.PropertyChanging += DefaultSetting_PropertyChanging;
		setting.PropertyChanged += DefaultSetting_PropertyChanged;
		return setting;
	}

	private static void DefaultSetting_PropertyChanging(object? sender, PropertyChangingEventArgs e)
	{
		if (e.PropertyName != nameof(DefaultSetting.SettingKey))
		{
			return;
		}
		if (sender is not DefaultSetting model)
		{
			return;
		}
		if (model.Pattern == null)
		{
			return;
		}
		var key = Reg.Open(nameof(Defaults));
		var settingKey = model.SettingKey;
		if (model.IsValid && key.GetValue(settingKey) != null)
		{
			key.DeleteValue(settingKey ?? string.Empty);
		}
	}

	private static void DefaultSetting_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not DefaultSetting model)
		{
			return;
		}
		var key = Reg.Open(nameof(Defaults));
		if (model.SettingKey == null)
		{
			return;
		}
		switch (e.PropertyName)
		{
			case nameof(DefaultSetting.Deleted) when model.Deleted:
				var settingKey = model.SettingKey;
				if (model.IsValid && key.GetValue(settingKey) != null)
				{
					key.DeleteValue(settingKey ?? string.Empty);
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

	private List<BrowserModel> GetBrowsers()
	{
		var list = Reg.SubKey(nameof(BrowserList));
		if (list == null)
		{
			return [];
		}

		var browsers = list.GetSubKeyNames()
			.Select(browser => GetBrowser(list, browser))
			.Where(browser => browser != null)
			.OrderByDescending(b => b!.Usage)!
			.ToList<BrowserModel>();

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

	private BrowserModel? GetBrowser(RegistryKey list, string name)
	{
		var config = list.OpenSubKey(name, false);
		if (config == null) return null;
		var browser = new BrowserModel
		{
			Name = name,
			Command = config.Get<string>(null, nameof(BrowserModel.Command)) ?? string.Empty,
			Executable = config.Get<string>(null, nameof(BrowserModel.Executable)),
			CommandArgs = config.Get<string>(null, nameof(BrowserModel.CommandArgs)),
			PrivacyArgs = config.Get<string>(null, nameof(BrowserModel.PrivacyArgs)),
			IconPath = config.Get<string>(null, nameof(BrowserModel.IconPath)),
			Usage = config.Get(0, nameof(BrowserModel.Usage)),
			Disabled = config.Get(false, nameof(BrowserModel.Disabled))
		};
		config.Close();
		if (string.IsNullOrWhiteSpace(browser.Command))
			return null;

		browser.PropertyChanged += BrowserConfiguration_PropertyChanged;
		return browser;
	}

	private void BrowserConfiguration_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not BrowserModel model)
		{
			return;
		}
		var config = Reg.EnsureSubKey(nameof(BrowserList), model.Name);
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
					Reg.SubKey(nameof(BrowserList))?.DeleteSubKey(model.Name);
					BrowserList.Remove(model);
					OnPropertyChanged(nameof(BrowserList));
				}
				break;
			default: return;
		}
	}

	private static readonly RegistryKey Reg = Registry.CurrentUser.Open("Software", nameof(BrowserPicker));
}