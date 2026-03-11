using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
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
	private const double MinWindowWidth = 320;
	private const double MinWindowHeight = 200;
	private const double DefaultConfigWindowWidth = 600;
	private const double DefaultConfigWindowHeight = 450;

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
	private bool auto_close_on_focus_lost = true;
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

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		AllowTrailingCommas = true,
		ReadCommentHandling = JsonCommentHandling.Skip
	};

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
	/// <param name="migrateFrom">When the JSON file does not exist, copy all settings from this source and save to JSON.</param>
	public JsonAppSettings(ILogger<JsonAppSettings> logger, IApplicationSettings? migrateFrom = null)
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
	private void MigrateFrom(IApplicationSettings source)
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
	public bool AutoCloseOnFocusLost { get => auto_close_on_focus_lost; set { if (SetProperty(ref auto_close_on_focus_lost, value)) SaveToFile(); } }
	public string[] UrlShorteners { get => url_shorteners; set { if (SetProperty(ref url_shorteners, value)) SaveToFile(); } }
	public bool AutoSizeWindow { get => auto_size_window; set { if (SetProperty(ref auto_size_window, value)) SaveToFile(); } }
	public double WindowWidth { get => window_width; set { var normalized = NormalizeMainWindowDimension(value, MinWindowWidth); if (SetProperty(ref window_width, normalized)) SaveToFile(); } }
	public double WindowHeight { get => window_height; set { var normalized = NormalizeMainWindowDimension(value, MinWindowHeight); if (SetProperty(ref window_height, normalized)) SaveToFile(); } }
	public double ConfigWindowWidth { get => config_window_width; set { var normalized = NormalizeConfigWindowDimension(value, MinWindowWidth, DefaultConfigWindowWidth); if (SetProperty(ref config_window_width, normalized)) SaveToFile(); } }
	public double ConfigWindowHeight { get => config_window_height; set { var normalized = NormalizeConfigWindowDimension(value, MinWindowHeight, DefaultConfigWindowHeight); if (SetProperty(ref config_window_height, normalized)) SaveToFile(); } }
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
		// Match both stable ids and display names so startup discovery merges with migrated
		// registry-backed entries whose ids may be legacy values like "ChromeHTML".
		var update = BrowserList.FirstOrDefault(m =>
			string.Equals(m.Id, model.Id, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(m.Id, model.Name, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(m.Name, model.Id, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(m.Name, model.Name, StringComparison.OrdinalIgnoreCase));
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
	public string ExportSettingsJson() => JsonSerializer.Serialize(CreateSerializableSettings(), JsonOptions);

	public void AppendBackupLog(string message)
	{
		BackupLog += $"{message}{Environment.NewLine}";
	}

	public async Task SaveAsync(string fileName)
	{
		try
		{
			await File.WriteAllTextAsync(fileName, ExportSettingsJson());
			AppendBackupLog($"Exported configuration to {fileName}");
		}
		catch (Exception e) { AppendBackupLog($"Unable to export configuration to {fileName}: {e.Message}"); }
	}

	public async Task LoadAsync(string fileName)
	{
		try
		{
			var text = await File.ReadAllTextAsync(fileName);
			TryImportSettingsJson(text, fileName);
		}
		catch (Exception ex) { AppendBackupLog($"Unable to read configuration from {fileName}: {ex.Message}"); }
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
		auto_close_on_focus_lost = s.AutoCloseOnFocusLost;
		url_shorteners = s.UrlShorteners;
		window_width = NormalizeMainWindowDimension(s.WindowWidth, MinWindowWidth);
		window_height = NormalizeMainWindowDimension(s.WindowHeight, MinWindowHeight);
		config_window_width = NormalizeConfigWindowDimension(s.ConfigWindowWidth, MinWindowWidth, DefaultConfigWindowWidth);
		config_window_height = NormalizeConfigWindowDimension(s.ConfigWindowHeight, MinWindowHeight, DefaultConfigWindowHeight);
		font_size = s.FontSize > 0 ? s.FontSize : 14;
		theme_mode = s.ThemeMode;
		EnsureSingleOrdering();
		OnPropertyChanged(nameof(AlwaysPrompt));
		OnPropertyChanged(nameof(AutoCloseOnFocusLost));
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
			existing.Name = browser.Name;
			existing.Disabled = browser.Disabled;
			existing.Executable = browser.Executable;
			existing.PrivacyArgs = browser.PrivacyArgs;
			existing.Usage = browser.Usage;
			existing.Command = browser.Command;
			existing.CommandArgs = browser.CommandArgs;
			existing.IconPath = browser.IconPath;
			existing.ManualOrder = browser.ManualOrder;
			existing.ExpandFileUrls = browser.ExpandFileUrls;
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
		var text = File.ReadAllText(settings_path);
		var settings = DeserializeSettings(text, settings_path);
		if (settings != null)
		{
			ApplyImportedSettings(settings);
		}
	}

	private void SaveToFile()
	{
		try
		{
			var dir = Path.GetDirectoryName(settings_path);
			if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
			File.WriteAllText(settings_path, ExportSettingsJson());
		}
		catch (Exception ex) { logger.LogWarning(ex, "Failed to save settings to {Path}", settings_path); }
	}

	public bool TryImportSettingsJson(string json, string sourceDescription)
	{
		var settings = DeserializeSettings(json, sourceDescription);
		if (settings == null)
		{
			return false;
		}

		ApplyImportedSettings(settings);
		AppendBackupLog($"Imported configuration from {sourceDescription}");
		SaveToFile();
		return true;
	}

	private SerializableSettings CreateSerializableSettings()
	{
		return new SerializableSettings(this)
		{
			AutoCloseOnFocusLost = auto_close_on_focus_lost,
			Schema = SerializableSettings.JsonSchemaUrl
		};
	}

	private SerializableSettings? DeserializeSettings(string json, string sourceDescription)
	{
		try
		{
			var root = JsonNode.Parse(UnwrapJsonDocument(json)) as JsonObject;
			if (root == null)
			{
				AppendBackupLog($"Unable to import configuration from {sourceDescription}: expected a JSON object.");
				return null;
			}

			if (!LooksLikeSettingsDocument(root))
			{
				AppendBackupLog($"Unable to import configuration from {sourceDescription}: contents are not BrowserPicker settings JSON.");
				return null;
			}

			var settings = root.Deserialize<SerializableSettings>(JsonOptions);
			if (settings == null)
			{
				AppendBackupLog($"Unable to import configuration from {sourceDescription}: document could not be deserialized.");
				return null;
			}

			return settings;
		}
		catch (JsonException ex)
		{
			AppendBackupLog($"Unable to import configuration from {sourceDescription}: {ex.Message}");
			return null;
		}
	}

	private void ApplyImportedSettings(SerializableSettings settings)
	{
		first_time = settings.FirstTime;
		UpdateSettings(settings);
		UpdateBrowsers(settings.BrowserList);
		UpdateDefaults(settings.Defaults);
		UpdateKeybindings(settings.KeyBindings);
	}

	private static bool LooksLikeSettingsDocument(JsonObject root)
	{
		if (root.TryGetPropertyValue("$schema", out var schemaNode)
			&& schemaNode is JsonValue schemaValue
			&& schemaValue.TryGetValue<string>(out var schema)
			&& string.Equals(schema, SerializableSettings.JsonSchemaUrl, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		string[] knownProperties =
		[
			nameof(SerializableSettings.BrowserList),
			nameof(SerializableSettings.Defaults),
			nameof(SerializableSettings.UrlShorteners),
			nameof(SerializableSettings.SortBy),
			nameof(SerializableSettings.AutoSizeWindow),
			nameof(SerializableSettings.AlwaysPrompt)
		];

		return knownProperties.Count(root.ContainsKey) >= 2;
	}

	private static string UnwrapJsonDocument(string text)
	{
		var trimmed = text.Trim();
		if (!trimmed.StartsWith("```", StringComparison.Ordinal))
		{
			return trimmed;
		}

		var firstLineBreak = trimmed.IndexOf('\n');
		var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
		if (firstLineBreak < 0 || lastFence <= firstLineBreak)
		{
			return trimmed;
		}

		return trimmed[(firstLineBreak + 1)..lastFence].Trim();
	}

	private static double NormalizeMainWindowDimension(double value, double minimum)
	{
		if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
		{
			return 0;
		}

		return Math.Max(minimum, Math.Round(value));
	}

	private static double NormalizeConfigWindowDimension(double value, double minimum, double fallback)
	{
		if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
		{
			return fallback;
		}

		return Math.Max(minimum, Math.Round(value));
	}
}
