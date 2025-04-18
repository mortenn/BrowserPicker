﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BrowserPicker.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace BrowserPicker.Windows;

public sealed class AppSettings : ModelBase, IBrowserPickerConfiguration
{
	private readonly ILogger<AppSettings> logger;

	public AppSettings(ILogger<AppSettings> logger)
	{
		this.logger = logger;
		sorter = new BrowserSorter(this);
		BrowserList = GetBrowsers();
		Defaults = GetDefaults();
		use_fallback_default = !string.IsNullOrWhiteSpace(Defaults.FirstOrDefault(d => d.Type == MatchType.Default)?.Browser);
	}

	/// <inheritdoc />
	public bool FirstTime
	{
		get => Reg.GetBool(true);
		set { Reg.Set(value); OnPropertyChanged(); }
	}

	/// <inheritdoc />
	public bool AlwaysPrompt
	{
		get => Reg.Get<bool>();
		set { Reg.Set(value); OnPropertyChanged(); }
	}

	/// <inheritdoc />
	public bool AlwaysUseDefaults
	{
		get => Reg.Get<bool>();
		set { Reg.Set(value); OnPropertyChanged(); }
	}

	/// <inheritdoc />
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

	/// <inheritdoc />
	public int UrlLookupTimeoutMilliseconds
	{
		get => Reg.Get(2000);
		set { Reg.Set(value); OnPropertyChanged(); }
	}

	/// <inheritdoc />
	public bool UseManualOrdering
	{
		get => Reg.Get<bool>();
		set
		{
			Reg.Set(value);
			UpdateOrder(value);
			OnPropertyChanged();
		}
	}

	/// <inheritdoc />
	public bool UseAutomaticOrdering
	{
		get => Reg.GetBool(true);
		set
		{
			Reg.Set(value);
			UpdateOrder(value);
			OnPropertyChanged();
		}
	}

	/// <inheritdoc />
	public bool UseAlphabeticalOrdering
	{
		get => Reg.Get<bool>();
		set
		{
			Reg.Set(value);
			UpdateOrder(value);
			OnPropertyChanged();
		}
	}

	private void UpdateOrder(bool value, [CallerMemberName] string? setting = null)
	{
		if (setting != nameof(UseAutomaticOrdering) && UseAutomaticOrdering && value)
		{
			UseAutomaticOrdering = false;
		}
		if (setting != nameof(UseManualOrdering) && UseManualOrdering && value)
		{
			UseManualOrdering = false;
		}
		if (setting != nameof(UseAlphabeticalOrdering) && UseAlphabeticalOrdering && value)
		{
			UseAlphabeticalOrdering = false;
		}
	}

	/// <inheritdoc />
	public bool DisableTransparency
	{
		get => Reg.Get<bool>();
		set { Reg.Set(value); OnPropertyChanged(); }
	}

	/// <inheritdoc />
	public bool DisableNetworkAccess
	{
		get => Reg.Get<bool>();
		set { Reg.Set(value); OnPropertyChanged(); }
	}

	/// <inheritdoc />
	public string[] UrlShorteners
	{
		get => Reg.Get<string[]>() ?? [];
		set { Reg.Set(value); OnPropertyChanged(); }
	}

	/// <inheritdoc />
	public List<BrowserModel> BrowserList
	{
		get;
	}

	/// <inheritdoc />
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
		key.Set(browser.ExpandFileUrls, nameof(browser.ExpandFileUrls));
		key.Set(browser.ManualOverride, nameof(browser.ManualOverride));
		
		var keyBind = Reg.Open(nameof(browser.CustomKeyBind));
		keyBind.Set(browser.Name, browser.CustomKeyBind);
		foreach (var other in BrowserList.Where(other => other.CustomKeyBind == browser.CustomKeyBind))
		{
			other.CustomKeyBind = string.Empty;
		}
		browser.PropertyChanged += BrowserConfiguration_PropertyChanged;

		BrowserList.Add(browser);
		logger.LogBrowserAdded(browser.Name);
		OnPropertyChanged(nameof(BrowserList));
	}

	/// <inheritdoc />
	public List<DefaultSetting> Defaults
	{
		get;
	}

	/// <inheritdoc />
	public List<KeyBinding> KeyBindings
	{
		get
		{
			var keyBind = Reg.Open(nameof(BrowserModel.CustomKeyBind));
			return (
				from key in keyBind.GetValueNames()
				where key != string.Empty
				let browser = keyBind.Get<string>(null, key)
				where browser != null
				select new KeyBinding(key, browser)
			).ToList();
		}
	}

	private bool use_fallback_default;
	private string backup_log = string.Empty;
	private readonly BrowserSorter sorter;

	/// <inheritdoc />
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

	/// <inheritdoc />
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

	/// <inheritdoc />
	public void AddDefault(MatchType matchType, string pattern, string? browser)
	{
		var setting = GetDefaultSetting(null, browser);
		if (setting == null)
		{
			return;
		}
		setting.Type = matchType;
		setting.Pattern = pattern;
		Defaults.Add(setting);
		logger.LogDefaultSettingAdded(matchType.ToString(), pattern, browser);
		OnPropertyChanged(nameof(Defaults));
	}

	/// <inheritdoc />
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

	/// <inheritdoc />
	public Task Start(CancellationToken cancellationToken)
	{
		return Task.Run(FindBrowsers, cancellationToken);
	}

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true
	};

	/// <inheritdoc />
	public async Task SaveAsync(string fileName)
	{
		var settings = new SerializableSettings(this);
		try
		{
			await Task.CompletedTask;
			await using var fileStream = File.Open(fileName, FileMode.Create, FileAccess.Write);
			await JsonSerializer.SerializeAsync(fileStream, settings, JsonOptions);
			BackupLog += $"Exported configuration to {fileName}\n";
		}
		catch(Exception e)
		{
			BackupLog += $"Unable to parse backup file: {e.Message}";
		}
	}

	/// <inheritdoc />
	public async Task LoadAsync(string fileName)
	{
		await using var file = File.OpenRead(fileName);
		SerializableSettings? settings;
		try
		{
			settings = await JsonSerializer.DeserializeAsync<SerializableSettings>(file, JsonOptions);
		}
		catch (Exception ex)
		{
			BackupLog += $"Unable to parse backup file: {ex.Message}";
			return;
		}
		if (settings == null)
		{
			BackupLog += "Unable to load backup";
			return;
		}

		UpdateSettings(settings);
		UpdateBrowsers(settings.BrowserList);
		UpdateDefaults(settings.Defaults);
		UpdateKeybinds(settings.KeyBindings);

		BackupLog += $"Imported configuration from {fileName}\n";
	}

	private void UpdateSettings(SerializableSettings settings)
	{
		AlwaysPrompt = settings.AlwaysPrompt;
		AlwaysUseDefaults = settings.AlwaysUseDefaults;
		AlwaysAskWithoutDefault = settings.AlwaysAskWithoutDefault;
		UrlLookupTimeoutMilliseconds = settings.UrlLookupTimeoutMilliseconds;
		UseAutomaticOrdering = settings.UseAutomaticOrdering;
		DisableTransparency = settings.DisableTransparency;
		DisableNetworkAccess = settings.DisableNetworkAccess;
	}

	/// <inheritdoc />
	public string BackupLog
	{
		get => backup_log;
		private set => SetProperty(ref backup_log, value);
	}

	/// <inheritdoc />
	public IComparer<BrowserModel> BrowserSorter => sorter;

	private void UpdateBrowsers(List<BrowserModel> browserList)
	{
		foreach (var browser in browserList)
		{
			var existing = BrowserList.FirstOrDefault(b => !b.Removed && b.Name == browser.Name);
			if (existing == null || existing.Removed)
			{
				AddBrowser(browser);
				continue;
			}
			existing.Disabled = browser.Disabled;
			existing.Executable = browser.Executable;
			existing.PrivacyArgs = browser.PrivacyArgs;
			existing.Usage = browser.Usage;
			existing.Command = browser.Command;
			existing.CommandArgs = browser.CommandArgs;
			existing.IconPath = browser.IconPath;
			existing.ManualOverride = browser.ManualOverride;
			existing.CustomKeyBind = browser.CustomKeyBind;
		}

		foreach (var browser in BrowserList.Where(b => browserList.All(s => s.Name != b.Name)).ToArray())
		{
			browser.Removed = true;
			BrowserList.Remove(browser);
		}
		OnPropertyChanged(nameof(BrowserList));
	}

	private void UpdateDefaults(List<DefaultSetting> defaults)
	{
		var fallback = defaults.FirstOrDefault(d => d.Type == MatchType.Default);
		UseFallbackDefault = fallback?.Browser != null;
		DefaultBrowser = fallback?.Browser;

		// Add or update defaults
		foreach (var setting in defaults)
		{
			if (setting == fallback)
			{
				continue;
			}
			var existing = Defaults.FirstOrDefault(d => d.SettingKey == setting.SettingKey);
			if (existing == null)
			{
				var newSetting = new DefaultSetting(setting.Type, setting.Pattern, null);
				newSetting.PropertyChanging += DefaultSetting_PropertyChanging;
				newSetting.PropertyChanged += DefaultSetting_PropertyChanged;
				Defaults.Add(newSetting);
				newSetting.Browser = setting.Browser;
				continue;
			}
			existing.Type = setting.Type;
			existing.Pattern = setting.Pattern;
			existing.Browser = setting.Browser;
		}

		// Remove defaults
		foreach (var setting in Defaults.Where(d => defaults.All(s => s.SettingKey != d.SettingKey)))
		{
			setting.Deleted = true;
		}
		OnPropertyChanged(nameof(Defaults));
	}

	private void UpdateKeybinds(List<KeyBinding> keyBindings)
	{
		foreach (var binding in keyBindings)
		{
			var browser = BrowserList.FirstOrDefault(b => b.Name == binding.Browser);
			if (browser == null)
			{
				continue;
			}
			browser.CustomKeyBind = binding.Key;
		}
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
			if (update.ManualOverride)
			{
				return;
			}
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
			.OfType<BrowserModel>()
			.OrderBy(v => v, sorter)
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

	private BrowserModel? GetBrowser(RegistryKey list, string name)
	{
		var config = list.OpenSubKey(name, false);
		var keyBind = Reg.Open(nameof(BrowserModel.CustomKeyBind));
		if (config == null) return null;
		var browser = new BrowserModel
		{
			Name = name,
			Command = config.Get<string>(null, nameof(BrowserModel.Command)) ?? string.Empty,
			Executable = config.Get<string>(null, nameof(BrowserModel.Executable)),
			CommandArgs = config.Get<string>(null, nameof(BrowserModel.CommandArgs)),
			PrivacyArgs = config.Get<string>(null, nameof(BrowserModel.PrivacyArgs)),
			IconPath = config.Get<string>(null, nameof(BrowserModel.IconPath)),
			ManualOrder = config.Get(0, nameof(BrowserModel.ManualOrder)),
			Usage = config.Get(0, nameof(BrowserModel.Usage)),
			Disabled = config.GetBool(false, nameof(BrowserModel.Disabled)),
			ExpandFileUrls = config.GetBool(false, nameof(BrowserModel.ExpandFileUrls)),
			ManualOverride = config.GetBool(false, nameof(BrowserModel.ManualOverride)),
			CustomKeyBind = keyBind.GetValueNames().FirstOrDefault(v => keyBind.Get<string>(null, v) == name) ?? string.Empty
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
			case nameof(BrowserModel.ManualOrder): config.Set(model.ManualOrder, e.PropertyName); break;
			case nameof(BrowserModel.ExpandFileUrls): config.Set(model.ExpandFileUrls, e.PropertyName); break;
			case nameof(BrowserModel.ManualOverride): config.Set(model.ManualOverride, e.PropertyName); break;
			case nameof(BrowserModel.Removed):
				if (model.Removed)
				{
					model.PropertyChanged -= BrowserConfiguration_PropertyChanged;
					Reg.SubKey(nameof(BrowserList))?.DeleteSubKey(model.Name);
					BrowserList.Remove(model);
					logger.LogBrowserRemoved(model.Name);
					OnPropertyChanged(nameof(BrowserList));
				}
				break;
			case nameof(BrowserModel.CustomKeyBind):
				var keyBind = Reg.Open(nameof(model.CustomKeyBind));
				var remove =
					from binding in keyBind.GetValueNames()
					where keyBind.Get<string>(null, binding) == model.Name
					select binding;
				foreach (var binding in remove)
				{
					keyBind.DeleteValue(binding);
				}
				keyBind.Set(model.Name, model.CustomKeyBind);
				foreach (var other in BrowserList.Where(other => other != model && other.CustomKeyBind == model.CustomKeyBind))
				{
					other.CustomKeyBind = string.Empty;
				}
				break;
			default: return;
		}
	}

	private static readonly RegistryKey Reg = Registry.CurrentUser.Open("Software", nameof(BrowserPicker));
}
