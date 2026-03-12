using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace BrowserPicker.Windows;

/// <summary>
/// Application settings snapshot backed by the Windows registry.
/// Read-only: used only to migrate existing settings to JSON; never writes to the registry.
/// </summary>
public sealed class AppSettings : IApplicationSettings
{
	/// <summary>
	/// Initializes settings from the registry (read-only) and sets up browser list and defaults.
	/// </summary>
	public AppSettings()
	{
		if (Reg != null)
		{
			FirstTime = Reg.GetBool(true);
			AlwaysPrompt = Reg.Get<bool>();
			AlwaysUseDefaults = Reg.Get<bool>();
			AlwaysAskWithoutDefault = Reg.Get<bool>();
			UrlLookupTimeoutMilliseconds = Reg.Get(2000);
			SortBy = Reg.Get<bool>(name: nameof(UseManualOrdering))
				? SerializableSettings.SortOrder.Manual
				: Reg.Get<bool>(name: nameof(UseAlphabeticalOrdering))
					? SerializableSettings.SortOrder.Alphabetical
					: SerializableSettings.SortOrder.Automatic;
			DisableTransparency = Reg.Get<bool>();
			DisableNetworkAccess = Reg.Get<bool>();
			UrlShorteners = Reg.Get<string[]>() ?? [];
			WindowWidth = double.TryParse(Reg.GetValue("WindowWidth") as string, out var w) ? w : 0;
			WindowHeight = double.TryParse(Reg.GetValue("WindowHeight") as string, out var h) ? h : 0;
			AutoSizeWindow = WindowWidth <= 0 && WindowHeight <= 0;
			FontSize = double.TryParse(Reg.GetValue("FontSize") as string, out var f) && f > 0 ? f : 14;
			ThemeMode = (ThemeMode)(Reg.GetValue("ThemeMode") is int i ? i : (int)ThemeMode.System);
		}

		var sorter = new BrowserSorter(this);
		BrowserList = GetBrowsers(sorter);
		Defaults = GetDefaults();
	}

	/// <inheritdoc />
	public bool FirstTime { get; set; }

	/// <inheritdoc />
	public bool AlwaysPrompt { get; set; }

	/// <inheritdoc />
	public bool AlwaysUseDefaults { get; set; } = true;

	/// <inheritdoc />
	public bool AlwaysAskWithoutDefault { get; set; }

	/// <inheritdoc />
	public int UrlLookupTimeoutMilliseconds { get; set; } = 2000;

	/// <inheritdoc />
	public SerializableSettings.SortOrder SortBy { get; set; } = SerializableSettings.SortOrder.Automatic;

	/// <inheritdoc />
	public bool UseManualOrdering
	{
		get => SortBy == SerializableSettings.SortOrder.Manual;
		set
		{
			if (value)
			{
				SortBy = SerializableSettings.SortOrder.Manual;
			}
		}
	}

	/// <inheritdoc />
	public bool UseAutomaticOrdering
	{
		get => SortBy == SerializableSettings.SortOrder.Automatic;
		set
		{
			if (value)
			{
				SortBy = SerializableSettings.SortOrder.Automatic;
			}
		}
	}

	/// <inheritdoc />
	public bool UseAlphabeticalOrdering
	{
		get => SortBy == SerializableSettings.SortOrder.Alphabetical;
		set
		{
			if (value)
			{
				SortBy = SerializableSettings.SortOrder.Alphabetical;
			}
		}
	}

	/// <inheritdoc />
	public bool DisableTransparency { get; set; }

	/// <inheritdoc />
	public double WindowOpacity { get; set; } = 0.92;

	/// <inheritdoc />
	public bool DisableNetworkAccess { get; set; }

	/// <inheritdoc />
	public string[] UrlShorteners { get; set; } = [];

	/// <inheritdoc />
	public List<BrowserModel> BrowserList { get; }

	/// <inheritdoc />
	public List<DefaultSetting> Defaults { get; }

	/// <inheritdoc />
	public List<KeyBinding> KeyBindings =>
		BrowserList
			.Where(b => !string.IsNullOrEmpty(b.CustomKeyBind))
			.Select(b => new KeyBinding(b.CustomKeyBind, b.Id))
			.ToList();

	/// <inheritdoc />
	public bool AutoSizeWindow { get; set; } = true;

	/// <inheritdoc />
	public double WindowWidth { get; set; }

	/// <inheritdoc />
	public double WindowHeight { get; set; }

	/// <inheritdoc />
	public double ConfigWindowWidth { get; set; } = 600;

	/// <inheritdoc />
	public double ConfigWindowHeight { get; set; } = 450;

	/// <inheritdoc />
	public double FontSize { get; set; } = 14;

	/// <inheritdoc />
	public ThemeMode ThemeMode { get; set; } = ThemeMode.System;

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
		foreach (var pattern in valueNames.Where(p => p != "|Default|"))
		{
			if (key.GetValue(pattern) is not string value) continue;
			var browser = BrowserList.FirstOrDefault(b => b.Id == value) ?? BrowserList.FirstOrDefault(b => b.Name == value);
			var browserId = browser?.Id ?? value;
			var setting = GetDefaultSetting(pattern, browserId);
			if (setting != null) list.Add(setting);
		}
		return list;
	}

	private static DefaultSetting? GetDefaultSetting(string? key, string? value)
	{
		if (value == null)
		{
			return null;
		}
		var setting = DefaultSetting.Decode(key, value);
		return setting ?? null;
	}

	private List<BrowserModel> GetBrowsers(BrowserSorter sorter)
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
		if (!browsers.Any(b => b.Name.Equals("Microsoft Edge", StringComparison.Ordinal)))
		{
			return browsers;
		}

		var edge = browsers.FirstOrDefault(b => b.Name.Equals("Edge", StringComparison.Ordinal));
		if (edge != null)
			browsers.Remove(edge);
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

		return browser;
	}

	/// <summary>Read-only registry key for migration; never written to.</summary>
	private static readonly RegistryKey? Reg = Registry.CurrentUser.OpenSubKey(@"Software\BrowserPicker", false);
}
