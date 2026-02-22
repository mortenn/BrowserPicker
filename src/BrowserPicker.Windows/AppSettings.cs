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
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace BrowserPicker.Windows;

/// <summary>
/// Application configuration backed by the Windows registry; implements <see cref="IBrowserPickerConfiguration"/>.
/// Read-only: used only to migrate existing registry data to JSON; never writes to the registry.
/// </summary>
public sealed class AppSettings : ModelBase, IBrowserPickerConfiguration
{
	private readonly ILogger<AppSettings> logger;

	// Backing fields for read-only registry snapshot (no writes)
	private bool first_time;
	private bool always_prompt;
	private bool always_use_defaults = true;
	private bool always_ask_without_default;
	private int url_lookup_timeout_ms = 2000;
	private bool use_manual_ordering;
	private bool use_automatic_ordering = true;
	private bool use_alphabetical_ordering;
	private bool disable_transparency;
	private bool disable_network_access;
	private string[] url_shorteners = [];
	private bool auto_size_window = true;
	private double window_width;
	private double window_height;
	private double config_window_width = 600;
	private double config_window_height = 450;
	private double font_size = 14;
	private ThemeMode theme_mode = ThemeMode.System;

	/// <summary>
	/// Initializes settings from the registry (read-only) and sets up browser list and defaults.
	/// </summary>
	/// <param name="logger">Logger for configuration operations.</param>
	public AppSettings(ILogger<AppSettings> logger)
	{
		this.logger = logger;
		sorter = new BrowserSorter(this);
		BrowserList = GetBrowsers();
		Defaults = GetDefaults();
		use_fallback_default = !string.IsNullOrWhiteSpace(Defaults.FirstOrDefault(d => d.Type == MatchType.Default)?.Browser);

		if (Reg != null)
		{
			first_time = Reg.GetBool(true);
			always_prompt = Reg.Get<bool>();
			always_use_defaults = Reg.Get<bool>();
			always_ask_without_default = Reg.Get<bool>();
			url_lookup_timeout_ms = Reg.Get(2000);
			use_manual_ordering = Reg.Get<bool>();
			use_automatic_ordering = Reg.GetBool(true);
			use_alphabetical_ordering = Reg.Get<bool>();
			disable_transparency = Reg.Get<bool>();
			disable_network_access = Reg.Get<bool>();
			url_shorteners = Reg.Get<string[]>() ?? [];
			window_width = double.TryParse(Reg.GetValue("WindowWidth") as string, out var w) ? w : 0;
			window_height = double.TryParse(Reg.GetValue("WindowHeight") as string, out var h) ? h : 0;
			auto_size_window = window_width <= 0 && window_height <= 0;
			font_size = double.TryParse(Reg.GetValue("FontSize") as string, out var f) && f > 0 ? f : 14;
			theme_mode = (ThemeMode)(Reg.GetValue("ThemeMode") is int i ? i : (int)ThemeMode.System);
		}
	}

	/// <inheritdoc />
	public bool FirstTime { get => first_time; set { first_time = value; OnPropertyChanged(); } }

	/// <inheritdoc />
	public bool AlwaysPrompt { get => always_prompt; set { always_prompt = value; OnPropertyChanged(); } }

	/// <inheritdoc />
	public bool AlwaysUseDefaults { get => always_use_defaults; set { always_use_defaults = value; OnPropertyChanged(); } }

	/// <inheritdoc />
	public bool AlwaysAskWithoutDefault
	{
		get => always_ask_without_default;
		set
		{
			always_ask_without_default = value;
			OnPropertyChanged();
			if (value && use_fallback_default)
				UseFallbackDefault = false;
		}
	}

	/// <inheritdoc />
	public int UrlLookupTimeoutMilliseconds { get => url_lookup_timeout_ms; set { url_lookup_timeout_ms = value; OnPropertyChanged(); } }

	/// <inheritdoc />
	public bool UseManualOrdering { get => use_manual_ordering; set { use_manual_ordering = value; UpdateOrder(value); OnPropertyChanged(); } }

	/// <inheritdoc />
	public bool UseAutomaticOrdering { get => use_automatic_ordering; set { use_automatic_ordering = value; UpdateOrder(value); OnPropertyChanged(); } }

	/// <inheritdoc />
	public bool UseAlphabeticalOrdering { get => use_alphabetical_ordering; set { use_alphabetical_ordering = value; UpdateOrder(value); OnPropertyChanged(); } }

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
	public bool DisableTransparency { get => disable_transparency; set { disable_transparency = value; OnPropertyChanged(); } }

	/// <inheritdoc />
	public bool DisableNetworkAccess { get => disable_network_access; set { disable_network_access = value; OnPropertyChanged(); } }

	/// <inheritdoc />
	public string[] UrlShorteners { get => url_shorteners; set { url_shorteners = value; OnPropertyChanged(); } }

	/// <inheritdoc />
	public bool AutoSizeWindow { get => auto_size_window; set { auto_size_window = value; OnPropertyChanged(); } }

	/// <inheritdoc />
	public double WindowWidth { get => window_width; set { window_width = value; OnPropertyChanged(); } }

	/// <inheritdoc />
	public double WindowHeight { get => window_height; set { window_height = value; OnPropertyChanged(); } }

	/// <inheritdoc />
	public double ConfigWindowWidth { get => config_window_width; set { config_window_width = value; OnPropertyChanged(); } }

	/// <inheritdoc />
	public double ConfigWindowHeight { get => config_window_height; set { config_window_height = value; OnPropertyChanged(); } }

	/// <inheritdoc />
	public double FontSize { get => font_size; set { font_size = value; OnPropertyChanged(); } }

	/// <inheritdoc />
	public ThemeMode ThemeMode { get => theme_mode; set { theme_mode = value; OnPropertyChanged(); } }

	/// <inheritdoc />
	public List<BrowserModel> BrowserList
	{
		get;
	}

	/// <inheritdoc />
	public void AddBrowser(BrowserModel browser)
	{
		browser.Id = string.IsNullOrEmpty(browser.Id) ? browser.Name : browser.Id;
		foreach (var other in BrowserList.Where(other => other.CustomKeyBind == browser.CustomKeyBind))
			other.CustomKeyBind = string.Empty;
		browser.PropertyChanged += BrowserConfiguration_PropertyChanged;
		BrowserList.Add(browser);
		logger.LogBrowserAdded(browser.Name);
		OnPropertyChanged(nameof(BrowserList));
	}

	/// <inheritdoc />
	public void PersistBrowser(BrowserModel browser)
	{
		// Read-only: no registry write.
	}

	/// <inheritdoc />
	public List<DefaultSetting> Defaults
	{
		get;
	}

	/// <inheritdoc />
	public List<KeyBinding> KeyBindings =>
		BrowserList
			.Where(b => !string.IsNullOrEmpty(b.CustomKeyBind))
			.Select(b => new KeyBinding(b.CustomKeyBind, b.Id))
			.ToList();

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
		foreach (var model in BrowserDiscovery.FindBrowsers())
		{
			AddOrUpdateBrowserModel(model);
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
			var detail = string.IsNullOrEmpty(ex.InnerException?.Message) ? ex.Message : $"{ex.Message} {ex.InnerException.Message}";
			BackupLog += $"Unable to parse backup file: {detail}";
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
		WindowWidth = settings.WindowWidth;
		WindowHeight = settings.WindowHeight;
		ConfigWindowWidth = settings.ConfigWindowWidth;
		ConfigWindowHeight = settings.ConfigWindowHeight;
		FontSize = settings.FontSize;
		ThemeMode = settings.ThemeMode;
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
			browser.Id = string.IsNullOrEmpty(browser.Id) ? browser.Name : browser.Id;
			var existing = BrowserList.FirstOrDefault(b => !b.Removed && (b.Id == browser.Id || b.Name == browser.Name));
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

		foreach (var browser in BrowserList.Where(b => browserList.All(s => s.Id != b.Id && s.Name != b.Name)).ToArray())
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
		// Normalize to Id so UI and launch path use Id (backup may contain name)
		var fallbackBrowserId = fallback?.Browser != null
			? BrowserList.FirstOrDefault(b => b.Id == fallback.Browser || b.Name == fallback.Browser)?.Id ?? fallback.Browser
			: null;
		DefaultBrowser = fallbackBrowserId;

		// Add or update defaults (normalize Browser to Id when loading from backup)
		foreach (var setting in defaults)
		{
			if (setting == fallback)
			{
				continue;
			}
			var browserId = string.IsNullOrEmpty(setting.Browser) ? null
				: BrowserList.FirstOrDefault(b => b.Id == setting.Browser || b.Name == setting.Browser)?.Id ?? setting.Browser;
			var existing = Defaults.FirstOrDefault(d => d.SettingKey == setting.SettingKey);
			if (existing == null)
			{
				var newSetting = new DefaultSetting(setting.Type, setting.Pattern, null);
				newSetting.PropertyChanging += DefaultSetting_PropertyChanging;
				newSetting.PropertyChanged += DefaultSetting_PropertyChanged;
				Defaults.Add(newSetting);
				newSetting.Browser = browserId;
				continue;
			}
			existing.Type = setting.Type;
			existing.Pattern = setting.Pattern;
			existing.Browser = browserId;
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
			var browser = BrowserList.FirstOrDefault(b => b.Id == binding.Browser || b.Name == binding.Browser);
			if (browser == null)
			{
				continue;
			}
			browser.CustomKeyBind = binding.Key;
		}
	}

	private void AddOrUpdateBrowserModel(BrowserModel model)
	{
		var update = BrowserList.FirstOrDefault(m => string.Equals(m.Id, model.Name, StringComparison.CurrentCultureIgnoreCase));
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

	private List<DefaultSetting> GetDefaults()
	{
		if (Reg == null) return [];
		using var key = Reg.OpenSubKey(nameof(Defaults), false);
		if (key == null) return [];
		var valueNames = key.GetValueNames();
		var list = new List<DefaultSetting>();
		// Read-only: treat legacy |Default| as default (empty pattern); do not mutate registry.
		if (valueNames.Contains("|Default|") && key.GetValue("|Default|") is string legacyDefault)
		{
			var browserId = BrowserList.FirstOrDefault(b => b.Id == legacyDefault || b.Name == legacyDefault)?.Id ?? legacyDefault;
			var setting = GetDefaultSetting(string.Empty, browserId);
			if (setting != null) list.Add(setting);
		}
		foreach (var pattern in valueNames.Where(p => p != "|Default|" && p != null))
		{
			var value = key.GetValue(pattern) as string;
			if (value == null) continue;
			var browser = BrowserList.FirstOrDefault(b => b.Id == value) ?? BrowserList.FirstOrDefault(b => b.Name == value);
			var browserId = browser?.Id ?? value;
			var setting = GetDefaultSetting(pattern, browserId);
			if (setting != null) list.Add(setting);
		}
		return list;
	}

	private DefaultSetting? GetDefaultSetting(string? key, string? value)
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

	private void DefaultSetting_PropertyChanging(object? sender, PropertyChangingEventArgs e)
	{
		// Read-only: no registry writes.
	}

	private void DefaultSetting_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not DefaultSetting model) return;
		// Read-only: only unsubscribe when deleted; no registry writes.
		if (e.PropertyName == nameof(DefaultSetting.Deleted) && model.Deleted)
		{
			model.PropertyChanging -= DefaultSetting_PropertyChanging;
			model.PropertyChanged -= DefaultSetting_PropertyChanged;
		}
	}

	private List<BrowserModel> GetBrowsers()
	{
		if (Reg == null) return [];
		using var list = Reg.OpenSubKey(nameof(BrowserList), false);
		if (list == null) return [];

		var browsers = list.GetSubKeyNames()
			.Select(browser => GetBrowser(list, browser))
			.OfType<BrowserModel>()
			.OrderBy(v => v, sorter)
			.ToList();

		// Read-only: remove legacy Edge from list only; do not delete from registry.
		if (browsers.Any(b => b.Name.Equals("Microsoft Edge", StringComparison.Ordinal)))
		{
			var edge = browsers.FirstOrDefault(b => b.Name.Equals("Edge", StringComparison.Ordinal));
			if (edge != null)
				browsers.Remove(edge);
		}
		return browsers;
	}

	private BrowserModel? GetBrowser(RegistryKey list, string keyName)
	{
		using var config = list.OpenSubKey(keyName, false);
		if (config == null) return null;
		using var keyBind = Reg?.OpenSubKey(nameof(BrowserModel.CustomKeyBind), false);
		var browser = new BrowserModel
		{
			Id = keyName,
			Name = config.Get<string>(null, nameof(BrowserModel.Name)) ?? keyName,
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
			CustomKeyBind = keyBind != null ? keyBind.GetValueNames().FirstOrDefault(v => keyBind.Get<string>(null, v) == keyName) ?? string.Empty : string.Empty
		};
		if (string.IsNullOrWhiteSpace(browser.Command))
			return null;

		browser.PropertyChanged += BrowserConfiguration_PropertyChanged;
		return browser;
	}

	private void BrowserConfiguration_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not BrowserModel model) return;
		// Read-only: only handle Removed (update in-memory list); no registry writes.
		if (e.PropertyName == nameof(BrowserModel.Removed) && model.Removed)
		{
			model.PropertyChanged -= BrowserConfiguration_PropertyChanged;
			BrowserList.Remove(model);
			logger.LogBrowserRemoved(model.Name);
			OnPropertyChanged(nameof(BrowserList));
		}
		else if (e.PropertyName == nameof(BrowserModel.CustomKeyBind))
		{
			foreach (var other in BrowserList.Where(other => other != model && other.CustomKeyBind == model.CustomKeyBind))
				other.CustomKeyBind = string.Empty;
		}
	}

	/// <summary>Read-only registry key for migration; never written to.</summary>
	private static readonly RegistryKey? Reg = Registry.CurrentUser.OpenSubKey(@"Software\BrowserPicker", false);
}
