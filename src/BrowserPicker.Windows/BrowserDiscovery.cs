using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace BrowserPicker.Windows;

/// <summary>
/// Scans the Windows registry for installed browsers. Used by both registry-backed and JSON-backed configuration.
/// </summary>
public static class BrowserDiscovery
{
	/// <summary>
	/// Enumerates all browsers registered in the system (StartMenuInternet and legacy Edge). Caller merges into their configuration.
	/// </summary>
	public static List<BrowserModel> FindBrowsers()
	{
		var list = new List<BrowserModel>();
		var byId = new Dictionary<string, BrowserModel>(StringComparer.OrdinalIgnoreCase);

		EnumerateBrowsers(Registry.LocalMachine, @"SOFTWARE\Clients\StartMenuInternet", AddOrUpdate);
		EnumerateBrowsers(Registry.CurrentUser, @"SOFTWARE\Clients\StartMenuInternet", AddOrUpdate);
		EnumerateBrowsers(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Clients\StartMenuInternet", AddOrUpdate);
		EnumerateBrowsers(Registry.CurrentUser, @"SOFTWARE\WOW6432Node\Clients\StartMenuInternet", AddOrUpdate);

		if (list.Any(b => b.Name.Contains("Edge", StringComparison.OrdinalIgnoreCase)))
		{
			return list;
		}

		var legacy = FindLegacyEdge();
		if (legacy != null)
			AddOrUpdate(legacy);

		return list;

		void AddOrUpdate(BrowserModel model)
		{
			var id = string.IsNullOrEmpty(model.Id) ? model.Name : model.Id;
			if (string.IsNullOrWhiteSpace(id))
				return;
			if (byId.TryGetValue(id, out var existing))
			{
				existing.Command = model.Command;
				existing.CommandArgs = model.CommandArgs;
				existing.PrivacyArgs = model.PrivacyArgs;
				existing.IconPath = model.IconPath;
			}
			else
			{
				byId[id] = model;
				list.Add(model);
			}
		}
	}

	private static void EnumerateBrowsers(RegistryKey hive, string subKey, Action<BrowserModel> addOrUpdate)
	{
		using var root = hive.OpenSubKey(subKey, false);
		if (root == null)
			return;
		foreach (var browser in root.GetSubKeyNames().Where(n => n != "BrowserPicker"))
		{
			var model = GetBrowserDetails(root, browser);
			if (model != null)
				addOrUpdate(model);
		}
	}

	private static BrowserModel? GetBrowserDetails(RegistryKey root, string browser)
	{
		using var reg = root.OpenSubKey(browser, false);
		if (reg == null)
			return null;

		var (name, icon, shell) = reg.GetBrowser();
		if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(shell))
			return null;

		var known = WellKnownBrowsers.Lookup(name, shell);
		return known != null
			? new BrowserModel(known, icon, shell)
			: new BrowserModel(name, icon, shell);
	}

	private static BrowserModel? FindLegacyEdge()
	{
		var systemApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SystemApps");
		if (!Directory.Exists(systemApps))
			return null;

		var targets = Directory.GetDirectories(systemApps, "*MicrosoftEdge_*");
		if (targets.Length == 0)
			return null;

		var known = WellKnownBrowsers.Lookup("Edge", null);
		if (known == null)
			return null;

		var appId = Path.GetFileName(targets[0]);
		var icon = Path.Combine(targets[0], "Assets", "MicrosoftEdgeSquare44x44.targetsize-32_altform-unplated.png");
		var shell = $"shell:AppsFolder\\{appId}!MicrosoftEdge";
		return new BrowserModel(known, icon, shell);
	}
}
