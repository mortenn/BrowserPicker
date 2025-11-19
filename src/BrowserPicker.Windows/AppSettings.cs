using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BrowserPicker.Framework;
using Microsoft.Win32;

namespace BrowserPicker.Windows;

public sealed class AppSettings : ModelBase, IBrowserPickerConfiguration
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AppSettings"/> class and loads persisted browsers, defaults and sorter.
	/// </summary>
	public AppSettings()
	{
		sorter = new BrowserSorter(this);
		BrowserList = GetBrowsers();
		Defaults = GetDefaults();
		use_fallback_default = !string.IsNullOrWhiteSpace(Defaults.FirstOrDefault(d => d.Type == MatchType.Default)?.Browser);
	}

	/// <summary>
	/// Indicates whether this is the first time the application has been run.
	/// Stored in the user registry.
	/// </summary>
	public bool FirstTime
	{
		get => Reg.GetBool(true);
		set { Reg.Set(value); OnPropertyChanged(); }
	}

	/// <summary>
	/// When true, always prompt the user to choose a browser instead of automatically selecting one.
	/// </summary>
	public bool AlwaysPrompt
	{
		get => Reg.Get<bool>();
		set { Reg.Set(value); OnPropertyChanged(); }
	}

	/// <summary>
	/// When true, always use configured defaults rather than attempting to auto-select a running browser.
	/// </summary>
	public bool AlwaysUseDefaults
	{
		get => Reg.Get<bool>();
		set { Reg.Set(value); OnPropertyChanged(); }
	}

	/// <summary>
	/// When true and there is no matching default, the user will be asked to choose a browser.
	/// </summary>
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

	/// <summary>
	/// Timeout in milliseconds for looking up and resolving URLs.
	/// </summary>
	public int UrlLookupTimeoutMilliseconds
	{
		get => Reg.Get(2000);
		set { Reg.Set(value); OnPropertyChanged(); }
	}

	/// <summary>
	/// When true, the user can manually order the list of browsers. Mutually exclusive with other ordering modes.
	/// </summary>
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

	/// <summary>
	/// When true, the application will order browsers automatically based on usage.
	/// </summary>
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

	/// <summary>
	/// When true, the application will order browsers alphabetically.
	/// </summary>
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

	/// <summary>
	/// Update other ordering settings to ensure mutual exclusivity when one ordering setting changes.
	/// </summary>
	/// <param name="value">The new value being applied to the calling ordering property.</param>
	/// <param name="setting">The name of the calling property (automatically provided).</param>
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

	/// <summary>
	/// When true, transparency is disabled for the UI.
	/// </summary>
	public bool DisableTransparency
	{
		get => Reg.Get<bool>();
		set { Reg.Set(value); OnPropertyChanged(); }
	}

	/// <summary>
	/// When true, network access features are disabled.
	/// </summary>
	public bool DisableNetworkAccess
	{
		get => Reg.Get<bool>();
		set { Reg.Set(value); OnPropertyChanged(); }
	}

	/// <summary>
	/// Hosts a list of known URL shortener host names.
	/// </summary>
	public string[] UrlShorteners
	{
		get => Reg.Get<string[]>() ?? [];
		set { Reg.Set(value); OnPropertyChanged(); }
	}

	/// <summary>
	/// The in-memory list of discovered and configured browsers.
	/// </summary>
	public List<BrowserModel> BrowserList
	{
		get;
	}

	/// <summary>
	/// Adds a browser to the persisted browser list and wires up property change handling.
	/// </summary>
	/// <param name="browser">The browser model to add.</param>
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
		browser.PropertyChanged += BrowserConfiguration_PropertyChanged;

		BrowserList.Add(browser);
		OnPropertyChanged(nameof(BrowserList));
	}

	public List<DefaultSetting> Defaults
	{
		get;
	}

	private bool use_fallback_default;
	private string backup_log = string.Empty;
	private readonly BrowserSorter sorter;

	/// <summary>
	/// When true, only URLs matching some <see cref="Defaults"/> record will give the user a choice.
	/// This makes BrowserPicker only apply for certain URLs.
	/// </summary>
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

	/// <summary>
	/// The browser identifier used as a fallback when <see cref="UseFallbackDefault"/> is true.
	/// </summary>
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

	/// <summary>
	/// Adds a default setting rule to the Defaults collection.
	/// </summary>
	/// <param name="matchType">The type of match to use for the default rule.</param>
	/// <param name="pattern">The pattern used to match URLs.</param>
	/// <param name="browser">The browser identifier to use when the rule matches.</param>
	/// <returns>The created <see cref="DefaultSetting"/> or null when creation fails.</returns>
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

	/// <summary>
	/// Scans the Windows registry for installed browsers and detects legacy Edge when appropriate.
	/// </summary>
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

	/// <summary>
	/// Starts long running background tasks for the settings object.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token to stop the operation.</param>
	/// <returns>A task representing the running operation.</returns>
	public Task Start(CancellationToken cancellationToken)
	{
		return Task.Run(FindBrowsers, cancellationToken);
	}

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true
	};

	/// <summary>
	/// Saves the current configuration to the specified JSON file asynchronously.
	/// </summary>
	/// <param name="fileName">The full path to the file to save.</param>
	/// <returns>A task that completes when the save operation finishes.</returns>
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

	/// <summary>
	/// Loads configuration from the specified JSON file asynchronously and updates current settings.
	/// </summary>
	/// <param name="fileName">The full path to the file to load.</param>
	/// <returns>A task that completes when the load operation finishes.</returns>
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

		BackupLog += $"Imported configuration from {fileName}\n";
	}

	/// <summary>
	/// Apply settings from an <see cref="IApplicationSettings"/> instance to this settings instance.
	/// </summary>
	/// <param name="settings">Source settings to apply.</param>
	private void UpdateSettings(IApplicationSettings settings)
	{
		AlwaysPrompt = settings.AlwaysPrompt;
		AlwaysUseDefaults = settings.AlwaysUseDefaults;
		AlwaysAskWithoutDefault = settings.AlwaysAskWithoutDefault;
		UrlLookupTimeoutMilliseconds = settings.UrlLookupTimeoutMilliseconds;
		UseAutomaticOrdering = settings.UseAutomaticOrdering;
		DisableTransparency = settings.DisableTransparency;
		DisableNetworkAccess = settings.DisableNetworkAccess;
	}

	/// <summary>
	/// Log from backup and restore operations.
	/// </summary>
	public string BackupLog
	{
		get => backup_log;
		private set => SetProperty(ref backup_log, value);
	}

	/// <summary>
	/// The comparer used to sort browsers in the UI.
	/// </summary>
	public IComparer<BrowserModel> BrowserSorter => sorter;

	/// <summary>
	/// Update the current browser list using a list imported from a backup, merging, adding and removing as needed.
	/// </summary>
	/// <param name="browserList">The list of browsers to merge into the current list.</param>
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
		}

		foreach (var browser in BrowserList.Where(b => browserList.All(s => s.Name != b.Name)).ToArray())
		{
			browser.Removed = true;
			BrowserList.Remove(browser);
		}
		OnPropertyChanged(nameof(BrowserList));
	}

	/// <summary>
	/// Update the default rules collection with rules imported from a backup.
	/// </summary>
	/// <param name="defaults">The list of defaults to merge into the current defaults.</param>
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

	/// <summary>
	/// Detects the legacy Windows 10 Edge app and adds it as a browser model when the modern Edge is not present.
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
		var model = new BrowserModel
		{
			Name = known.Name,
			Command = shell,
			Executable = known.RealExecutable,
			PrivacyArgs = known.PrivacyArgs,
			IconPath = icon
		};
		AddOrUpdateBrowserModel(model);
	}

	/// <summary>
	/// Enumerates subkeys under the specified registry path and attempts to create browser models for each entry.
	/// </summary>
	/// <param name="hive">The root registry hive to search.</param>
	/// <param name="subKey">The subkey path to enumerate.</param>
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

	/// <summary>
	/// Reads browser metadata from the specified registry subkey and returns a browser model when valid.
	/// </summary>
	/// <param name="root">Registry key containing the browser entry.</param>
	/// <param name="browser">The subkey name for the browser entry.</param>
	/// <returns>A <see cref="BrowserModel"/> when a valid browser is found; otherwise null.</returns>
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
			? new BrowserModel
			{
				Name = known.Name,
				Command = shell,
				Executable = known.RealExecutable,
				PrivacyArgs = known.PrivacyArgs,
				IconPath = icon
			}
			: new BrowserModel
			{
				Name = name,
				Command = shell,
				IconPath = icon
			};
	}

	/// <summary>
	/// Adds a new browser model to the list or updates an existing model with the same name.
	/// </summary>
	/// <param name="model">The browser model to add or update.</param>
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

	/// <summary>
	/// Loads default rules from registry, converting legacy default entries when present.
	/// </summary>
	/// <returns>A list of <see cref="DefaultSetting"/> representing persisted defaults.</returns>
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

	/// <summary>
	/// Converts an old-style default registry value into the current format and returns the updated value names.
	/// </summary>
	/// <param name="key">Registry key containing the defaults.</param>
	/// <returns>The updated list of value names after conversion.</returns>
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

	/// <summary>
	/// Creates a <see cref="DefaultSetting"/> from a registry key/value pair and wires up change notifications.
	/// </summary>
	/// <param name="key">The registry key name for the default (may be null).</param>
	/// <param name="value">The registry stored value to decode.</param>
	/// <returns>The decoded <see cref="DefaultSetting"/>, or null if unable to decode.</returns>
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

	/// <summary>
	/// Handles <see cref="DefaultSetting.PropertyChanging"/> events to remove legacy registry values when keys change.
	/// </summary>
	/// <param name="sender">Event sender, expected to be a <see cref="DefaultSetting"/>.</param>
	/// <param name="e">Property changing event args.</param>
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

	/// <summary>
	/// Handles <see cref="DefaultSetting.PropertyChanged"/> events to persist changes to the registry or remove deleted rules.
	/// </summary>
	/// <param name="sender">Event sender, expected to be a <see cref="DefaultSetting"/>.</param>
	/// <param name="e">Property changed event args.</param>
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

	/// <summary>
	/// Reads the persisted list of browsers from the registry and returns them sorted.
	/// </summary>
	/// <returns>A list of <see cref="BrowserModel"/> instances loaded from the registry.</returns>
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

	/// <summary>
	/// Reads a single browser configuration from the given registry key.
	/// </summary>
	/// <param name="list">The registry key containing the browser entries.</param>
	/// <param name="name">The subkey name for the browser.</param>
	/// <returns>A <see cref="BrowserModel"/> when the entry is valid; otherwise null.</returns>
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
			ManualOrder = config.Get(0, nameof(BrowserModel.ManualOrder)),
			Usage = config.Get(0, nameof(BrowserModel.Usage)),
			Disabled = config.GetBool(false, nameof(BrowserModel.Disabled)),
			ExpandFileUrls = config.GetBool(false, nameof(BrowserModel.ExpandFileUrls))
		};
		config.Close();
		if (string.IsNullOrWhiteSpace(browser.Command))
			return null;

		browser.PropertyChanged += BrowserConfiguration_PropertyChanged;
		return browser;
	}

	/// <summary>
	/// Persists property changes from a <see cref="BrowserModel"/> to the registry and handles removal.
	/// </summary>
	/// <param name="sender">The browser model that changed.</param>
	/// <param name="e">The property change event args.</param>
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
