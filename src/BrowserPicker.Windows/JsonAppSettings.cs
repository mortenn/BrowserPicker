using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BrowserPicker.Framework;
using Microsoft.Extensions.Logging;

namespace BrowserPicker.Windows;

/// <summary>
/// Application configuration backed by a JSON file; implements <see cref="IBrowserPickerConfiguration"/>.
/// Uses %LocalAppData%\BrowserPicker\settings.json when the file exists; otherwise registry-backed config is used.
/// </summary>
public sealed class JsonAppSettings : ModelBase, IBrowserPickerConfiguration
{
	private readonly ILogger<JsonAppSettings> _logger;
	private readonly string _settingsPath;
	private readonly BrowserSorter _sorter;
	private readonly List<BrowserModel> _browserList;
	private readonly List<DefaultSetting> _defaults;
	private bool _firstTime = true;
	private bool _alwaysPrompt;
	private bool _alwaysUseDefaults = true;
	private bool _alwaysAskWithoutDefault;
	private int _urlLookupTimeoutMilliseconds = 2000;
	private bool _useManualOrdering;
	private bool _useAutomaticOrdering = true;
	private bool _useAlphabeticalOrdering;
	private bool _disableTransparency;
	private bool _disableNetworkAccess;
	private string[] _urlShorteners = [];
	private bool _useFallbackDefault;
	private string _backupLog = string.Empty;
	private bool _autoSizeWindow = true;
	private double _windowWidth;
	private double _windowHeight;
	private double _configWindowWidth = 600;
	private double _configWindowHeight = 450;
	private double _fontSize = 14;
	private ThemeMode _themeMode = ThemeMode.System;

	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	/// <summary>
	/// Full path to the JSON settings file (%LocalAppData%\BrowserPicker\settings.json).
	/// </summary>
	public static string GetSettingsFilePath()
	{
		var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), nameof(BrowserPicker));
		return Path.Combine(dir, "settings.json");
	}

	/// <summary>
	/// True if the JSON settings file exists; used by DI to choose JSON vs registry backend.
	/// </summary>
	public static bool SettingsFileExists() => File.Exists(GetSettingsFilePath());

	/// <summary>
	/// Initializes JSON-backed settings. If the JSON file does not exist and <paramref name="migrateFrom"/> is provided,
	/// copies configuration from the registry (or other source) into JSON and uses it as the source of truth.
	/// </summary>
	/// <param name="logger">Logger for configuration operations.</param>
	/// <param name="migrateFrom">When the JSON file does not exist, copy all settings from this configuration and save to JSON.</param>
	public JsonAppSettings(ILogger<JsonAppSettings> logger, IBrowserPickerConfiguration? migrateFrom = null)
	{
		_logger = logger;
		_settingsPath = GetSettingsFilePath();
		_sorter = new BrowserSorter(this);
		_browserList = [];
		_defaults = [];

		if (File.Exists(_settingsPath))
		{
			try
			{
				LoadFromFile();
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to load settings from {Path}; using defaults", _settingsPath);
			}
		}
		else if (migrateFrom != null)
		{
			MigrateFrom(migrateFrom);
		}

		_useFallbackDefault = !string.IsNullOrWhiteSpace(_defaults.FirstOrDefault(d => d.Type == MatchType.Default)?.Browser);

		// When we migrated, UpdateDefaults and AddBrowser already attached handlers; otherwise attach now (e.g. after LoadFromFile).
		var alreadySubscribed = migrateFrom != null;
		if (!alreadySubscribed)
		{
			foreach (var d in _defaults)
			{
				d.PropertyChanging += DefaultSetting_PropertyChanging;
				d.PropertyChanged += DefaultSetting_PropertyChanged;
			}
			foreach (var b in _browserList)
				b.PropertyChanged += Browser_PropertyChanged;
		}
	}

	/// <summary>
	/// Copies configuration from an existing source (e.g. registry) and saves to the JSON file.
	/// </summary>
	private void MigrateFrom(IBrowserPickerConfiguration source)
	{
		var snapshot = new SerializableSettings(source);
		UpdateSettings(snapshot);
		UpdateBrowsers(snapshot.BrowserList);
		UpdateDefaults(snapshot.Defaults);
		UpdateKeybinds(snapshot.KeyBindings);
		SaveToFile();
		_logger.LogInformation("Migrated configuration from registry to {Path}", _settingsPath);
	}

	public bool FirstTime { get => _firstTime; set { if (SetProperty(ref _firstTime, value)) SaveToFile(); } }
	public bool AlwaysPrompt { get => _alwaysPrompt; set { if (SetProperty(ref _alwaysPrompt, value)) SaveToFile(); } }
	public bool AlwaysUseDefaults { get => _alwaysUseDefaults; set { if (SetProperty(ref _alwaysUseDefaults, value)) SaveToFile(); } }
	public bool AlwaysAskWithoutDefault { get => _alwaysAskWithoutDefault; set { if (SetProperty(ref _alwaysAskWithoutDefault, value)) SaveToFile(); if (value && _useFallbackDefault) UseFallbackDefault = false; } }
	public int UrlLookupTimeoutMilliseconds { get => _urlLookupTimeoutMilliseconds; set { if (SetProperty(ref _urlLookupTimeoutMilliseconds, value)) SaveToFile(); } }
	public bool UseManualOrdering { get => _useManualOrdering; set { if (SetProperty(ref _useManualOrdering, value)) { UpdateOrder(); SaveToFile(); } } }
	public bool UseAutomaticOrdering { get => _useAutomaticOrdering; set { if (SetProperty(ref _useAutomaticOrdering, value)) { UpdateOrder(); SaveToFile(); } } }
	public bool UseAlphabeticalOrdering { get => _useAlphabeticalOrdering; set { if (SetProperty(ref _useAlphabeticalOrdering, value)) { UpdateOrder(); SaveToFile(); } } }
	public bool DisableTransparency { get => _disableTransparency; set { if (SetProperty(ref _disableTransparency, value)) SaveToFile(); } }
	public bool DisableNetworkAccess { get => _disableNetworkAccess; set { if (SetProperty(ref _disableNetworkAccess, value)) SaveToFile(); } }
	public string[] UrlShorteners { get => _urlShorteners; set { if (SetProperty(ref _urlShorteners, value)) SaveToFile(); } }
	public bool AutoSizeWindow { get => _autoSizeWindow; set { if (SetProperty(ref _autoSizeWindow, value)) SaveToFile(); } }
	public double WindowWidth { get => _windowWidth; set { if (SetProperty(ref _windowWidth, value)) SaveToFile(); } }
	public double WindowHeight { get => _windowHeight; set { if (SetProperty(ref _windowHeight, value)) SaveToFile(); } }
	public double ConfigWindowWidth { get => _configWindowWidth; set { if (SetProperty(ref _configWindowWidth, value)) SaveToFile(); } }
	public double ConfigWindowHeight { get => _configWindowHeight; set { if (SetProperty(ref _configWindowHeight, value)) SaveToFile(); } }
	public double FontSize { get => _fontSize; set { if (SetProperty(ref _fontSize, value)) SaveToFile(); } }
	public ThemeMode ThemeMode { get => _themeMode; set { if (SetProperty(ref _themeMode, value)) SaveToFile(); } }

	public List<BrowserModel> BrowserList => _browserList;
	public List<DefaultSetting> Defaults => _defaults;

	public List<KeyBinding> KeyBindings =>
		_browserList
			.Where(b => !string.IsNullOrEmpty(b.CustomKeyBind))
			.Select(b => new KeyBinding(b.CustomKeyBind, b.Id))
			.ToList();

	public bool UseFallbackDefault
	{
		get => _useFallbackDefault;
		set
		{
			if (value == _useFallbackDefault) return;
			if (value) { AlwaysAskWithoutDefault = false; _useFallbackDefault = true; if (_defaults.All(d => d.Type != MatchType.Default)) AddDefault(MatchType.Default, string.Empty, null); }
			else { _useFallbackDefault = false; DefaultBrowser = null; }
			OnPropertyChanged();
			SaveToFile();
		}
	}

	public string? DefaultBrowser
	{
		get => _defaults.FirstOrDefault(d => d.Type == MatchType.Default)?.Browser;
		set
		{
			var selection = _defaults.FirstOrDefault(d => d.Type == MatchType.Default);
			if (selection == null && !string.IsNullOrWhiteSpace(value))
			{
				selection = GetDefaultSetting(string.Empty, string.Empty)!;
				selection.Type = MatchType.Default;
				selection.PropertyChanging += DefaultSetting_PropertyChanging;
				selection.PropertyChanged += DefaultSetting_PropertyChanged;
				_defaults.Add(selection);
			}
			if (selection != null && value != selection.Browser) { selection.Browser = value; _useFallbackDefault = value != null; OnPropertyChanged(nameof(UseFallbackDefault)); }
			OnPropertyChanged();
			SaveToFile();
		}
	}

	public void AddBrowser(BrowserModel browser)
	{
		browser.Id = string.IsNullOrEmpty(browser.Id) ? browser.Name : browser.Id;
		foreach (var other in _browserList.Where(other => other.CustomKeyBind == browser.CustomKeyBind))
			other.CustomKeyBind = string.Empty;
		browser.PropertyChanged += Browser_PropertyChanged;
		_browserList.Add(browser);
		_logger.LogBrowserAdded(browser.Name);
		OnPropertyChanged(nameof(BrowserList));
		SaveToFile();
	}

	public void PersistBrowser(BrowserModel browser) => SaveToFile();

	public void FindBrowsers()
	{
		foreach (var model in BrowserDiscovery.FindBrowsers())
			AddOrUpdateBrowserModel(model);
	}

	private void AddOrUpdateBrowserModel(BrowserModel model)
	{
		var update = _browserList.FirstOrDefault(m => string.Equals(m.Id, model.Name, StringComparison.OrdinalIgnoreCase));
		if (update != null)
		{
			if (update.ManualOverride) return;
			update.Command = model.Command;
			update.CommandArgs = model.CommandArgs;
			update.PrivacyArgs = model.PrivacyArgs;
			update.IconPath = model.IconPath;
			SaveToFile();
			return;
		}
		AddBrowser(model);
	}

	public void AddDefault(MatchType matchType, string pattern, string? browser)
	{
		var setting = GetDefaultSetting(null, browser);
		if (setting == null) return;
		setting.Type = matchType;
		setting.Pattern = pattern;
		setting.PropertyChanging += DefaultSetting_PropertyChanging;
		setting.PropertyChanged += DefaultSetting_PropertyChanged;
		_defaults.Add(setting);
		_logger.LogDefaultSettingAdded(matchType.ToString(), pattern, browser);
		OnPropertyChanged(nameof(Defaults));
		SaveToFile();
	}

	public Task Start(CancellationToken cancellationToken) => Task.Run(FindBrowsers, cancellationToken);

	public string BackupLog { get => _backupLog; private set => SetProperty(ref _backupLog, value); }
	public IComparer<BrowserModel>? BrowserSorter => _sorter;

	public async Task SaveAsync(string fileName)
	{
		var settings = new SerializableSettings(this);
		try
		{
			await using var fs = File.Open(fileName, FileMode.Create, FileAccess.Write);
			await JsonSerializer.SerializeAsync(fs, settings, JsonOptions);
			BackupLog += $"Exported configuration to {fileName}\n";
		}
		catch (Exception e) { BackupLog += $"Unable to parse backup file: {e.Message}"; }
	}

	public async Task LoadAsync(string fileName)
	{
		await using var file = File.OpenRead(fileName);
		SerializableSettings? settings;
		try { settings = await JsonSerializer.DeserializeAsync<SerializableSettings>(file, JsonOptions); }
		catch (Exception ex) { BackupLog += $"Unable to parse backup file: {(ex.InnerException?.Message ?? ex.Message)}"; return; }
		if (settings == null) { BackupLog += "Unable to load backup"; return; }
		UpdateSettings(settings);
		UpdateBrowsers(settings.BrowserList);
		UpdateDefaults(settings.Defaults);
		UpdateKeybinds(settings.KeyBindings);
		BackupLog += $"Imported configuration from {fileName}\n";
		SaveToFile();
	}

	private void UpdateOrder()
	{
		if (_useAutomaticOrdering) { _useManualOrdering = false; _useAlphabeticalOrdering = false; }
		if (_useManualOrdering) { _useAutomaticOrdering = false; _useAlphabeticalOrdering = false; }
		if (_useAlphabeticalOrdering) { _useAutomaticOrdering = false; _useManualOrdering = false; }
	}

	private DefaultSetting? GetDefaultSetting(string? key, string? value)
	{
		if (value == null) return null;
		var setting = DefaultSetting.Decode(key, value);
		if (setting == null) return null;
		return setting;
	}

	private void UpdateSettings(SerializableSettings s)
	{
		_alwaysPrompt = s.AlwaysPrompt;
		_alwaysUseDefaults = s.AlwaysUseDefaults;
		_alwaysAskWithoutDefault = s.AlwaysAskWithoutDefault;
		_urlLookupTimeoutMilliseconds = s.UrlLookupTimeoutMilliseconds;
		_useAutomaticOrdering = s.UseAutomaticOrdering;
		_useManualOrdering = s.UseManualOrdering;
		_useAlphabeticalOrdering = s.UseAlphabeticalOrdering;
		_disableTransparency = s.DisableTransparency;
		_disableNetworkAccess = s.DisableNetworkAccess;
		_urlShorteners = s.UrlShorteners ?? [];
		_windowWidth = s.WindowWidth;
		_windowHeight = s.WindowHeight;
		_configWindowWidth = s.ConfigWindowWidth > 0 ? s.ConfigWindowWidth : 600;
		_configWindowHeight = s.ConfigWindowHeight > 0 ? s.ConfigWindowHeight : 450;
		_fontSize = s.FontSize > 0 ? s.FontSize : 14;
		_themeMode = s.ThemeMode;
		OnPropertyChanged(nameof(AlwaysPrompt));
		OnPropertyChanged(nameof(WindowWidth));
		OnPropertyChanged(nameof(WindowHeight));
		OnPropertyChanged(nameof(ConfigWindowWidth));
		OnPropertyChanged(nameof(ConfigWindowHeight));
		OnPropertyChanged(nameof(FontSize));
		OnPropertyChanged(nameof(ThemeMode));
	}

	private void UpdateBrowsers(List<BrowserModel> browserList)
	{
		foreach (var browser in browserList)
		{
			browser.Id = string.IsNullOrEmpty(browser.Id) ? browser.Name : browser.Id;
			var existing = _browserList.FirstOrDefault(b => !b.Removed && (b.Id == browser.Id || b.Name == browser.Name));
			if (existing == null || existing.Removed) { AddBrowser(browser); continue; }
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
		foreach (var b in _browserList.Where(b => browserList.All(s => s.Id != b.Id && s.Name != b.Name)).ToArray())
		{
			b.Removed = true;
			b.PropertyChanged -= Browser_PropertyChanged;
			_browserList.Remove(b);
		}
		OnPropertyChanged(nameof(BrowserList));
	}

	private void UpdateDefaults(List<DefaultSetting> defaults)
	{
		var fallback = defaults.FirstOrDefault(d => d.Type == MatchType.Default);
		_useFallbackDefault = fallback?.Browser != null;
		var fallbackId = fallback?.Browser != null ? _browserList.FirstOrDefault(b => b.Id == fallback.Browser || b.Name == fallback.Browser)?.Id ?? fallback.Browser : null;
		foreach (var d in _defaults) { d.PropertyChanging -= DefaultSetting_PropertyChanging; d.PropertyChanged -= DefaultSetting_PropertyChanged; }
		_defaults.Clear();
		if (fallbackId != null) { var def = new DefaultSetting(MatchType.Default, string.Empty, fallbackId); def.PropertyChanging += DefaultSetting_PropertyChanging; def.PropertyChanged += DefaultSetting_PropertyChanged; _defaults.Add(def); }
		foreach (var setting in defaults.Where(s => s != fallback))
		{
			var browserId = string.IsNullOrEmpty(setting.Browser) ? null : _browserList.FirstOrDefault(b => b.Id == setting.Browser || b.Name == setting.Browser)?.Id ?? setting.Browser;
			var newSetting = new DefaultSetting(setting.Type, setting.Pattern, null);
			newSetting.PropertyChanging += DefaultSetting_PropertyChanging;
			newSetting.PropertyChanged += DefaultSetting_PropertyChanged;
			_defaults.Add(newSetting);
			newSetting.Browser = browserId;
		}
		OnPropertyChanged(nameof(Defaults));
		OnPropertyChanged(nameof(UseFallbackDefault));
		OnPropertyChanged(nameof(DefaultBrowser));
	}

	private void UpdateKeybinds(List<KeyBinding> keyBindings)
	{
		foreach (var binding in keyBindings)
		{
			var browser = _browserList.FirstOrDefault(b => b.Id == binding.Browser || b.Name == binding.Browser);
			if (browser != null) browser.CustomKeyBind = binding.Key;
		}
	}

	private void DefaultSetting_PropertyChanging(object? sender, PropertyChangingEventArgs e) { }
	private void DefaultSetting_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is DefaultSetting) SaveToFile();
	}

	private void Browser_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is BrowserModel model && e.PropertyName == nameof(BrowserModel.Removed) && model.Removed)
		{
			model.PropertyChanged -= Browser_PropertyChanged;
			_browserList.Remove(model);
			_logger.LogBrowserRemoved(model.Name);
			OnPropertyChanged(nameof(BrowserList));
		}
		else if (sender != null)
			SaveToFile();
	}

	private void LoadFromFile()
	{
		using var stream = File.OpenRead(_settingsPath);
		var settings = JsonSerializer.Deserialize<SerializableSettings>(stream, JsonOptions);
		if (settings == null) return;
		_firstTime = settings.FirstTime;
		_alwaysPrompt = settings.AlwaysPrompt;
		_alwaysUseDefaults = settings.AlwaysUseDefaults;
		_alwaysAskWithoutDefault = settings.AlwaysAskWithoutDefault;
		_urlLookupTimeoutMilliseconds = settings.UrlLookupTimeoutMilliseconds;
		_useManualOrdering = settings.UseManualOrdering;
		_useAutomaticOrdering = settings.UseAutomaticOrdering;
		_useAlphabeticalOrdering = settings.UseAlphabeticalOrdering;
		_disableTransparency = settings.DisableTransparency;
		_disableNetworkAccess = settings.DisableNetworkAccess;
		_urlShorteners = settings.UrlShorteners ?? [];
		_autoSizeWindow = settings.WindowWidth <= 0 && settings.WindowHeight <= 0 ? true : settings.AutoSizeWindow;
		_windowWidth = settings.WindowWidth;
		_windowHeight = settings.WindowHeight;
		_configWindowWidth = settings.ConfigWindowWidth > 0 ? settings.ConfigWindowWidth : 600;
		_configWindowHeight = settings.ConfigWindowHeight > 0 ? settings.ConfigWindowHeight : 450;
		_fontSize = settings.FontSize > 0 ? settings.FontSize : 14;
		_themeMode = settings.ThemeMode;
		_browserList.Clear();
		foreach (var b in settings.BrowserList ?? [])
		{
			b.Id = string.IsNullOrEmpty(b.Id) ? b.Name : b.Id;
			b.PropertyChanged += Browser_PropertyChanged;
			_browserList.Add(b);
		}
		_defaults.Clear();
		foreach (var d in settings.Defaults ?? [])
		{
			var setting = new DefaultSetting(d.Type, d.Pattern ?? string.Empty, d.Browser);
			setting.PropertyChanging += DefaultSetting_PropertyChanging;
			setting.PropertyChanged += DefaultSetting_PropertyChanged;
			_defaults.Add(setting);
		}
		UpdateKeybinds(settings.KeyBindings ?? []);
	}

	private void SaveToFile()
	{
		try
		{
			var dir = Path.GetDirectoryName(_settingsPath);
			if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
			var settings = new SerializableSettings(this);
			using var stream = File.Create(_settingsPath);
			JsonSerializer.Serialize(stream, settings, JsonOptions);
		}
		catch (Exception ex) { _logger.LogWarning(ex, "Failed to save settings to {Path}", _settingsPath); }
	}
}
