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
	private readonly ILogger<JsonAppSettings> logger;
	private readonly string settings_path;
	private readonly BrowserSorter sorter;
	private bool first_time = true;
	private bool always_prompt;
	private bool always_use_defaults = true;
	private bool always_ask_without_default;
	private int url_lookup_timeout_milliseconds = 2000;
	private bool use_manual_ordering;
	private bool use_automatic_ordering = true;
	private bool use_alphabetical_ordering;
	private bool disable_transparency;
	private double window_opacity = 0.92;
	private bool disable_network_access;
	private string[] url_shorteners = [];
	private bool use_fallback_default;
	private string backup_log = string.Empty;
	private bool auto_size_window = true;
	private double window_width;
	private double window_height;
	private double config_window_width = 600;
	private double config_window_height = 450;
	private double font_size = 14;
	private ThemeMode theme_mode = ThemeMode.System;

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
		this.logger = logger;
		settings_path = GetSettingsFilePath();
		sorter = new BrowserSorter(this);
		BrowserList = [];
		Defaults = [];

		if (File.Exists(settings_path))
		{
			try
			{
				LoadFromFile();
			}
			catch (Exception ex)
			{
				this.logger.LogWarning(ex, "Failed to load settings from {Path}; using defaults", settings_path);
			}
		}
		else if (migrateFrom != null)
		{
			MigrateFrom(migrateFrom);
		}

		use_fallback_default = !string.IsNullOrWhiteSpace(Defaults.FirstOrDefault(d => d.Type == MatchType.Default)?.Browser);

		// When we migrated, UpdateDefaults and AddBrowser already attached handlers; otherwise attach now (e.g. after LoadFromFile).
		var alreadySubscribed = migrateFrom != null;
		if (alreadySubscribed)
		{
			return;
		}

		foreach (var d in Defaults)
		{
			d.PropertyChanged += DefaultSetting_PropertyChanged;
		}
		foreach (var b in BrowserList)
			b.PropertyChanged += Browser_PropertyChanged;
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
		UpdateKeybindings(snapshot.KeyBindings);
		SaveToFile();
		logger.LogInformation("Migrated configuration from registry to {Path}", settings_path);
	}

	public bool FirstTime { get => first_time; set { if (SetProperty(ref first_time, value)) SaveToFile(); } }
	public bool AlwaysPrompt { get => always_prompt; set { if (SetProperty(ref always_prompt, value)) SaveToFile(); } }
	public bool AlwaysUseDefaults { get => always_use_defaults; set { if (SetProperty(ref always_use_defaults, value)) SaveToFile(); } }
	public bool AlwaysAskWithoutDefault { get => always_ask_without_default; set { if (SetProperty(ref always_ask_without_default, value)) SaveToFile(); if (value && use_fallback_default) UseFallbackDefault = false; } }
	public int UrlLookupTimeoutMilliseconds { get => url_lookup_timeout_milliseconds; set { if (SetProperty(ref url_lookup_timeout_milliseconds, value)) SaveToFile(); } }
	public bool UseManualOrdering { get => use_manual_ordering; set { if (!SetProperty(ref use_manual_ordering, value)) return; if (value) { use_automatic_ordering = false; use_alphabetical_ordering = false; OnPropertyChanged(nameof(UseAutomaticOrdering)); OnPropertyChanged(nameof(UseAlphabeticalOrdering)); } SaveToFile(); } }
	public bool UseAutomaticOrdering { get => use_automatic_ordering; set { if (!SetProperty(ref use_automatic_ordering, value)) return; if (value) { use_manual_ordering = false; use_alphabetical_ordering = false; OnPropertyChanged(nameof(UseManualOrdering)); OnPropertyChanged(nameof(UseAlphabeticalOrdering)); } SaveToFile(); } }
	public bool UseAlphabeticalOrdering { get => use_alphabetical_ordering; set { if (!SetProperty(ref use_alphabetical_ordering, value)) return; if (value) { use_manual_ordering = false; use_automatic_ordering = false; OnPropertyChanged(nameof(UseManualOrdering)); OnPropertyChanged(nameof(UseAutomaticOrdering)); } SaveToFile(); } }
	public bool DisableTransparency { get => disable_transparency; set { if (SetProperty(ref disable_transparency, value)) SaveToFile(); } }
	public double WindowOpacity { get => window_opacity; set { var rounded = Math.Round(Math.Clamp(value, 0.5, 1.0), 2); if (SetProperty(ref window_opacity, rounded)) SaveToFile(); } }
	public bool DisableNetworkAccess { get => disable_network_access; set { if (SetProperty(ref disable_network_access, value)) SaveToFile(); } }
	public string[] UrlShorteners { get => url_shorteners; set { if (SetProperty(ref url_shorteners, value)) SaveToFile(); } }
	public bool AutoSizeWindow { get => auto_size_window; set { if (SetProperty(ref auto_size_window, value)) SaveToFile(); } }
	public double WindowWidth { get => window_width; set { if (SetProperty(ref window_width, value)) SaveToFile(); } }
	public double WindowHeight { get => window_height; set { if (SetProperty(ref window_height, value)) SaveToFile(); } }
	public double ConfigWindowWidth { get => config_window_width; set { if (SetProperty(ref config_window_width, value)) SaveToFile(); } }
	public double ConfigWindowHeight { get => config_window_height; set { if (SetProperty(ref config_window_height, value)) SaveToFile(); } }
	public double FontSize { get => font_size; set { if (SetProperty(ref font_size, value)) SaveToFile(); } }
	public ThemeMode ThemeMode { get => theme_mode; set { if (SetProperty(ref theme_mode, value)) SaveToFile(); } }

	public List<BrowserModel> BrowserList { get; }

	public List<DefaultSetting> Defaults { get; }

	public List<KeyBinding> KeyBindings =>
		BrowserList
			.Where(b => !string.IsNullOrEmpty(b.CustomKeyBind))
			.Select(b => new KeyBinding(b.CustomKeyBind, b.Id))
			.ToList();

	public bool UseFallbackDefault
	{
		get => use_fallback_default;
		set
		{
			if (value == use_fallback_default) return;
			if (value) { AlwaysAskWithoutDefault = false; use_fallback_default = true; if (Defaults.All(d => d.Type != MatchType.Default)) AddDefault(MatchType.Default, string.Empty, null); }
			else { use_fallback_default = false; DefaultBrowser = null; }
			OnPropertyChanged();
			SaveToFile();
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
				selection.PropertyChanged += DefaultSetting_PropertyChanged;
				Defaults.Add(selection);
			}
			if (selection != null && value != selection.Browser) { selection.Browser = value; use_fallback_default = value != null; OnPropertyChanged(nameof(UseFallbackDefault)); }
			OnPropertyChanged();
			SaveToFile();
		}
	}

	public void AddBrowser(BrowserModel browser)
	{
		browser.Id = string.IsNullOrEmpty(browser.Id) ? browser.Name : browser.Id;
		foreach (var other in BrowserList.Where(other => other.CustomKeyBind == browser.CustomKeyBind))
			other.CustomKeyBind = string.Empty;
		browser.PropertyChanged += Browser_PropertyChanged;
		BrowserList.Add(browser);
		logger.LogBrowserAdded(browser.Name);
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
		var update = BrowserList.FirstOrDefault(m => string.Equals(m.Id, model.Name, StringComparison.OrdinalIgnoreCase));
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
		setting.PropertyChanged += DefaultSetting_PropertyChanged;
		Defaults.Add(setting);
		logger.LogDefaultSettingAdded(matchType.ToString(), pattern, browser);
		OnPropertyChanged(nameof(Defaults));
		SaveToFile();
	}

	public Task Start(CancellationToken cancellationToken) => Task.Run(FindBrowsers, cancellationToken);

	public string BackupLog { get => backup_log; private set => SetProperty(ref backup_log, value); }
	public IComparer<BrowserModel> BrowserSorter => sorter;

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
		catch (Exception ex) { BackupLog += $"Unable to parse backup file: {ex.InnerException?.Message ?? ex.Message}"; return; }
		if (settings == null) { BackupLog += "Unable to load backup"; return; }
		UpdateSettings(settings);
		UpdateBrowsers(settings.BrowserList);
		UpdateDefaults(settings.Defaults);
		UpdateKeybindings(settings.KeyBindings);
		BackupLog += $"Imported configuration from {fileName}\n";
		SaveToFile();
	}

	/// <summary>Ensure exactly one of the three ordering options is true (after load from file).</summary>
	private void EnsureSingleOrdering()
	{
		var count = (use_automatic_ordering ? 1 : 0) + (use_manual_ordering ? 1 : 0) + (use_alphabetical_ordering ? 1 : 0);
		if (count == 1) return;
		use_automatic_ordering = true;
		use_manual_ordering = false;
		use_alphabetical_ordering = false;
		OnPropertyChanged(nameof(UseAutomaticOrdering));
		OnPropertyChanged(nameof(UseManualOrdering));
		OnPropertyChanged(nameof(UseAlphabeticalOrdering));
	}

	private static DefaultSetting? GetDefaultSetting(string? key, string? value)
	{
		if (value == null) return null;
		var setting = DefaultSetting.Decode(key, value);
		return setting ?? null;
	}

	private void UpdateSettings(SerializableSettings s)
	{
		always_prompt = s.AlwaysPrompt;
		always_use_defaults = s.AlwaysUseDefaults;
		always_ask_without_default = s.AlwaysAskWithoutDefault;
		url_lookup_timeout_milliseconds = s.UrlLookupTimeoutMilliseconds;
		use_automatic_ordering = s.UseAutomaticOrdering;
		use_manual_ordering = s.UseManualOrdering;
		use_alphabetical_ordering = s.UseAlphabeticalOrdering;
		disable_transparency = s.DisableTransparency;
		window_opacity = Math.Round(Math.Clamp(s.WindowOpacity, 0.5, 1.0), 2);
		disable_network_access = s.DisableNetworkAccess;
		url_shorteners = s.UrlShorteners;
		window_width = s.WindowWidth;
		window_height = s.WindowHeight;
		config_window_width = s.ConfigWindowWidth > 0 ? s.ConfigWindowWidth : 600;
		config_window_height = s.ConfigWindowHeight > 0 ? s.ConfigWindowHeight : 450;
		font_size = s.FontSize > 0 ? s.FontSize : 14;
		theme_mode = s.ThemeMode;
		EnsureSingleOrdering();
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
			var existing = BrowserList.FirstOrDefault(b => !b.Removed && (b.Id == browser.Id || b.Name == browser.Name));
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
		foreach (var b in BrowserList.Where(b => browserList.All(s => s.Id != b.Id && s.Name != b.Name)).ToArray())
		{
			b.Removed = true;
			b.PropertyChanged -= Browser_PropertyChanged;
			BrowserList.Remove(b);
		}
		OnPropertyChanged(nameof(BrowserList));
	}

	private void UpdateDefaults(List<DefaultSetting> defaults)
	{
		var fallback = defaults.FirstOrDefault(d => d.Type == MatchType.Default);
		use_fallback_default = fallback?.Browser != null;
		var fallbackId = fallback?.Browser != null ? BrowserList.FirstOrDefault(b => b.Id == fallback.Browser || b.Name == fallback.Browser)?.Id ?? fallback.Browser : null;
		foreach (var d in Defaults) { d.PropertyChanged -= DefaultSetting_PropertyChanged; }
		Defaults.Clear();
		if (fallbackId != null) { var def = new DefaultSetting(MatchType.Default, string.Empty, fallbackId); def.PropertyChanged += DefaultSetting_PropertyChanged; Defaults.Add(def); }
		foreach (var setting in defaults.Where(s => s != fallback))
		{
			var browserId = string.IsNullOrEmpty(setting.Browser) ? null : BrowserList.FirstOrDefault(b => b.Id == setting.Browser || b.Name == setting.Browser)?.Id ?? setting.Browser;
			var newSetting = new DefaultSetting(setting.Type, setting.Pattern, null);
			newSetting.PropertyChanged += DefaultSetting_PropertyChanged;
			Defaults.Add(newSetting);
			newSetting.Browser = browserId;
		}
		OnPropertyChanged(nameof(Defaults));
		OnPropertyChanged(nameof(UseFallbackDefault));
		OnPropertyChanged(nameof(DefaultBrowser));
	}

	private void UpdateKeybindings(List<KeyBinding> keyBindings)
	{
		foreach (var binding in keyBindings)
		{
			var browser = BrowserList.FirstOrDefault(b => b.Id == binding.Browser || b.Name == binding.Browser);
			if (browser != null) browser.CustomKeyBind = binding.Key;
		}
	}

	private void DefaultSetting_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is DefaultSetting) SaveToFile();
	}

	private void Browser_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is BrowserModel model && e.PropertyName == nameof(BrowserModel.Removed) && model.Removed)
		{
			model.PropertyChanged -= Browser_PropertyChanged;
			BrowserList.Remove(model);
			logger.LogBrowserRemoved(model.Name);
			OnPropertyChanged(nameof(BrowserList));
		}
		else if (sender != null)
			SaveToFile();
	}

	private void LoadFromFile()
	{
		using var stream = File.OpenRead(settings_path);
		var settings = JsonSerializer.Deserialize<SerializableSettings>(stream, JsonOptions);
		if (settings == null) return;
		first_time = settings.FirstTime;
		always_prompt = settings.AlwaysPrompt;
		always_use_defaults = settings.AlwaysUseDefaults;
		always_ask_without_default = settings.AlwaysAskWithoutDefault;
		url_lookup_timeout_milliseconds = settings.UrlLookupTimeoutMilliseconds;
		use_manual_ordering = settings.UseManualOrdering;
		use_automatic_ordering = settings.UseAutomaticOrdering;
		use_alphabetical_ordering = settings.UseAlphabeticalOrdering;
		disable_transparency = settings.DisableTransparency;
		window_opacity = Math.Round(Math.Clamp(settings.WindowOpacity, 0.5, 1.0), 2);
		disable_network_access = settings.DisableNetworkAccess;
		url_shorteners = settings.UrlShorteners;
		auto_size_window = settings is { WindowWidth: <= 0, WindowHeight: <= 0 } || settings.AutoSizeWindow;
		window_width = settings.WindowWidth;
		window_height = settings.WindowHeight;
		config_window_width = settings.ConfigWindowWidth > 0 ? settings.ConfigWindowWidth : 600;
		config_window_height = settings.ConfigWindowHeight > 0 ? settings.ConfigWindowHeight : 450;
		font_size = settings.FontSize > 0 ? settings.FontSize : 14;
		theme_mode = settings.ThemeMode;
		EnsureSingleOrdering();
		BrowserList.Clear();
		foreach (var b in settings.BrowserList)
		{
			b.Id = string.IsNullOrEmpty(b.Id) ? b.Name : b.Id;
			b.PropertyChanged += Browser_PropertyChanged;
			BrowserList.Add(b);
		}
		Defaults.Clear();
		foreach (var setting in from d in settings.Defaults
		         select new DefaultSetting(d.Type, d.Pattern ?? string.Empty, d.Browser))
		{
			setting.PropertyChanged += DefaultSetting_PropertyChanged;
			Defaults.Add(setting);
		}
		UpdateKeybindings(settings.KeyBindings);
	}

	private void SaveToFile()
	{
		try
		{
			var dir = Path.GetDirectoryName(settings_path);
			if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
			var settings = new SerializableSettings(this);
			using var stream = File.Create(settings_path);
			JsonSerializer.Serialize(stream, settings, JsonOptions);
		}
		catch (Exception ex) { logger.LogWarning(ex, "Failed to save settings to {Path}", settings_path); }
	}
}
