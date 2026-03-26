using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using BrowserPicker.Common;
using Microsoft.Extensions.Logging;

// ReSharper disable CommentTypo

namespace BrowserPicker.Windows.ProfileDiscovery;

/// <summary>
/// Discovers containers for Firefox-based browsers.
/// The active profile directory is determined by matching the browser's executable path
/// against <c>compatibility.ini</c> in each profile listed in <c>profiles.ini</c>.
/// Containers are read from <c>containers.json</c> in the matched profile.
/// </summary>
public static class FirefoxProfileDiscovery
{
	/// <summary>
	/// Discovers Firefox containers for the given browser installation.
	/// </summary>
	/// <param name="userDataPath">
	/// Relative path under <c>%AppData%</c> to the Firefox data directory
	/// (e.g. <c>Mozilla\Firefox</c>).
	/// </param>
	/// <param name="browserExePath">
	/// Full path to the browser executable, used to match the correct profile
	/// via <c>compatibility.ini</c>.
	/// </param>
	/// <param name="logger">Optional logger for diagnostic output.</param>
	/// <returns>List of discovered containers; empty if none found.</returns>
	public static List<BrowserProfile> Discover(string userDataPath, string? browserExePath, ILogger? logger = null)
	{
		var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), userDataPath);

		if (!Directory.Exists(root))
		{
			logger?.LogDebug("Firefox data directory not found: {Path}", root);
			return [];
		}

		var iniPath = Path.Combine(root, "profiles.ini");
		if (!File.Exists(iniPath))
		{
			logger?.LogDebug("profiles.ini not found at {Path}", iniPath);
			return [];
		}

		var profileEntries = ParseProfileEntries(iniPath, logger);
		var activeProfilePath = FindActiveProfilePath(root, profileEntries, browserExePath, logger);

		if (activeProfilePath != null)
		{
			return DiscoverContainers(activeProfilePath, logger);
		}

		logger?.LogDebug("Could not determine active Firefox profile for {Exe}", browserExePath);
		return [];
	}

	private record ProfileEntry(string Name, string Path, bool IsRelative);

	private static List<ProfileEntry> ParseProfileEntries(string iniPath, ILogger? logger)
	{
		var entries = new List<ProfileEntry>();
		try
		{
			var lines = File.ReadAllLines(iniPath);
			string? currentSection = null;
			string? currentName = null;
			string? currentPath = null;
			var isRelative = true;

			foreach (var rawLine in lines)
			{
				var line = rawLine.Trim();

				if (line.StartsWith('['))
				{
					FlushEntry(entries, currentSection, currentName, currentPath, isRelative);
					currentSection = line.TrimStart('[').TrimEnd(']');
					currentName = null;
					currentPath = null;
					isRelative = true;
					continue;
				}

				var eqIndex = line.IndexOf('=');
				if (eqIndex < 0)
					continue;
				var key = line[..eqIndex].Trim();
				var value = line[(eqIndex + 1)..].Trim();

				switch (key)
				{
					case "Name":
						currentName = value;
						break;
					case "Path":
						currentPath = value;
						break;
					case "IsRelative" when value == "0":
						isRelative = false;
						break;
				}
			}

			FlushEntry(entries, currentSection, currentName, currentPath, isRelative);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			logger?.LogDebug(ex, "Could not read profiles.ini at {Path}", iniPath);
		}

		return entries;
	}

	private static void FlushEntry(
		List<ProfileEntry> entries,
		string? section,
		string? name,
		string? path,
		bool isRelative
	)
	{
		if (
			section != null
			&& section.StartsWith("Profile", StringComparison.OrdinalIgnoreCase)
			&& name != null
			&& path != null
		)
		{
			entries.Add(new ProfileEntry(name, path, isRelative));
		}
	}

	/// <summary>
	/// Extracts the installation directory from a shell command string.
	/// Handles quoted paths like <c>"C:\Program Files\Firefox\firefox.exe" -osint -url "%1"</c>.
	/// </summary>
	private static string? ExtractInstallDirectory(string? shellCommand)
	{
		if (string.IsNullOrWhiteSpace(shellCommand))
			return null;

		string exePath;
		if (shellCommand.StartsWith('"'))
		{
			var endQuote = shellCommand.IndexOf('"', 1);
			exePath = endQuote > 1 ? shellCommand[1..endQuote] : shellCommand.Trim('"');
		}
		else
		{
			var exeIdx = shellCommand.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
			exePath = exeIdx >= 0 ? shellCommand[..(exeIdx + 4)] : shellCommand;
		}

		return Path.GetDirectoryName(exePath);
	}

	/// <summary>
	/// Finds the profile directory for this Firefox installation by checking
	/// <c>compatibility.ini</c> in each profile for a matching <c>LastPlatformDir</c>.
	/// Falls back to the first existing profile directory if no match is found.
	/// </summary>
	private static string? FindActiveProfilePath(
		string root,
		List<ProfileEntry> entries,
		string? browserExePath,
		ILogger? logger
	)
	{
		if (entries.Count == 0)
			return null;

		var installDir = ExtractInstallDirectory(browserExePath);

		if (installDir != null)
		{
			foreach (var entry in entries)
			{
				var fullPath = entry.IsRelative ? Path.Combine(root, entry.Path) : entry.Path;
				if (!Directory.Exists(fullPath))
					continue;

				var compatPath = Path.Combine(fullPath, "compatibility.ini");
				if (!File.Exists(compatPath))
					continue;

				try
				{
					var compatiblePlatform = (
						from line in File.ReadLines(compatPath)
						where line.StartsWith("LastPlatformDir=", StringComparison.OrdinalIgnoreCase)
						select line["LastPlatformDir=".Length..].Trim()
					).Any(platformDir => string.Equals(platformDir, installDir, StringComparison.OrdinalIgnoreCase));

					if (!compatiblePlatform)
						continue;

					logger?.LogDebug("Matched Firefox profile {Name} to installation {Dir}", entry.Name, installDir);
					return fullPath;
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					logger?.LogDebug(ex, "Could not read compatibility.ini at {Path}", compatPath);
				}
			}
		}

		// Fallback: use first profile directory that exists
		var fallback = entries
			.Select(e => e.IsRelative ? Path.Combine(root, e.Path) : e.Path)
			.FirstOrDefault(Directory.Exists);

		if (fallback != null)
			logger?.LogDebug("No compatibility.ini match for {Exe}, falling back to {Path}", browserExePath, fallback);

		return fallback;
	}

	/// <summary>
	/// Resolves a container name from a Firefox l10n ID.
	/// Predefined containers use IDs like <c>user-context-personal</c> (current format)
	/// or <c>userContextPersonal.label</c> (legacy format) instead of a <c>name</c> field.
	/// </summary>
	private static string? ResolveL10NName(string? l10NId)
	{
		if (string.IsNullOrWhiteSpace(l10NId))
			return null;

		// Current format: "user-context-personal" → "Personal"
		const string currentPrefix = "user-context-";
		if (l10NId.StartsWith(currentPrefix, StringComparison.Ordinal) && l10NId.Length > currentPrefix.Length)
		{
			var raw = l10NId[currentPrefix.Length..];
			return char.ToUpperInvariant(raw[0]) + raw[1..];
		}

		// Legacy format: "userContextPersonal.label" → "Personal"
		const string legacyPrefix = "userContext";
		const string legacySuffix = ".label";
		if (
			l10NId.StartsWith(legacyPrefix, StringComparison.Ordinal)
			&& l10NId.EndsWith(legacySuffix, StringComparison.Ordinal)
		)
		{
			return l10NId[legacyPrefix.Length..^legacySuffix.Length];
		}

		return null;
	}

	private static List<BrowserProfile> DiscoverContainers(string profilePath, ILogger? logger)
	{
		var containers = new List<BrowserProfile>();
		var containersFile = Path.Combine(profilePath, "containers.json");

		if (!File.Exists(containersFile))
		{
			return containers;
		}

		try
		{
			var json = File.ReadAllText(containersFile);
			var root = JsonNode.Parse(json);
			var identities = root?["identities"]?.AsArray();

			if (identities == null)
			{
				return containers;
			}

			foreach (var identity in identities)
			{
				if (identity == null)
					continue;

				var isPublic = identity["public"]?.GetValue<bool>() ?? true;
				if (!isPublic)
					continue;

				var name = identity["name"]?.GetValue<string>();

				if (string.IsNullOrWhiteSpace(name))
				{
					name = ResolveL10NName(identity["l10nId"]?.GetValue<string>());
				}

				if (string.IsNullOrWhiteSpace(name))
					continue;

				var color = identity["color"]?.GetValue<string>();
				var icon = identity["icon"]?.GetValue<string>();

				var id = $"container:{name}";
				var profile = new BrowserProfile(
					id: id,
					name: name,
					commandArgs: null,
					urlTemplate: $"ext+container:name={name}&url={{url}}"
				)
				{
					IconColor = string.IsNullOrEmpty(color) ? null : color,
					ContainerIcon = string.IsNullOrEmpty(icon) ? null : icon,
				};
				containers.Add(profile);

				logger?.LogDebug(
					"Discovered Firefox container: {Name} (color={Color}, icon={Icon})",
					name,
					color,
					icon
				);
			}
		}
		catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
		{
			logger?.LogDebug(ex, "Could not read containers.json at {Path}", containersFile);
		}

		return containers;
	}
}
