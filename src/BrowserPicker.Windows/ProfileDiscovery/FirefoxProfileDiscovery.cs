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
/// Discovers profiles and containers for Firefox-based browsers.
/// The profiles that belong to an installation are found by matching the browser's executable path
/// against <c>compatibility.ini</c> in each profile listed in <c>profiles.ini</c>.
/// Containers are read from <c>containers.json</c> in each of those profiles.
/// </summary>
/// <remarks>
/// <para>
/// With one profile, only its containers are returned (<c>container:Name</c>), as before.
/// </para>
/// <para>
/// With several profiles, each profile is returned (<c>profile:Name</c>, launched with <c>-P "Name"</c>)
/// followed by its containers, named <c>Profile › Container</c>. Containers of the installation's default
/// profile keep their <c>container:Name</c> id and launch without <c>-P</c>, so existing rules keep working;
/// containers of the other profiles use <c>profile:Profile/container:Name</c> and launch with <c>-P</c>.
/// </para>
/// <para>
/// A profile last run before Firefox 67 does not count when the installation has a dedicated profile: it is
/// the old <c>default</c> profile Firefox left behind. When the default profile is ambiguous (several profiles are
/// some installation's default), every entry launches with <c>-P</c> so links never open in the wrong profile.
/// </para>
/// </remarks>
public static class FirefoxProfileDiscovery
{
	/// <summary>Separates the profile name from the container name in display names.</summary>
	public const string ProfileContainerSeparator = " › ";

	/// <summary>
	/// Discovers Firefox profiles and containers for the given browser installation.
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
	/// <returns>List of discovered profiles and containers; empty if none found.</returns>
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

		var ini = ParseProfilesIni(iniPath, logger);
		var installed = DropLegacyProfiles(
			FindInstallationProfiles(root, ini.Profiles, browserExePath, logger),
			ini,
			logger
		);
		if (installed.Count > 1)
		{
			return DiscoverProfilesAndContainers(root, ini, installed, logger);
		}

		var activeProfilePath =
			installed.Count == 1
				? FullPath(root, installed[0])
				: FindFallbackProfilePath(root, ini.Profiles, browserExePath, logger);

		if (activeProfilePath != null)
		{
			return [.. ReadContainers(activeProfilePath, logger).Select(c => CreateContainer(c, null, true))];
		}

		logger?.LogDebug("Could not determine active Firefox profile for {Exe}", browserExePath);
		return [];
	}

	private record ProfileEntry(string Name, string Path, bool IsRelative, bool IsDefault)
	{
		/// <summary>Major Firefox version that last ran the profile, from <c>compatibility.ini</c>; null when unknown.</summary>
		public int? LastMajorVersion { get; init; }
	}

	/// <summary>Firefox 67 gave each installation its own dedicated profile.</summary>
	private const int DedicatedProfilesVersion = 67;

	private record ProfilesIni(List<ProfileEntry> Profiles, HashSet<string> InstallDefaults);

	private record ContainerIdentity(string Name, string? Color, string? Icon);

	private static string FullPath(string root, ProfileEntry entry) =>
		entry.IsRelative ? Path.Combine(root, entry.Path) : entry.Path;

	private static string NormalizeIniPath(string path) => path.Replace('\\', '/').TrimEnd('/');

	private static ProfilesIni ParseProfilesIni(string iniPath, ILogger? logger)
	{
		var entries = new List<ProfileEntry>();
		var installDefaults = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			var lines = File.ReadAllLines(iniPath);
			string? currentSection = null;
			string? currentName = null;
			string? currentPath = null;
			var isRelative = true;
			var isDefault = false;

			foreach (var rawLine in lines)
			{
				var line = rawLine.Trim();

				if (line.StartsWith('['))
				{
					FlushEntry(entries, currentSection, currentName, currentPath, isRelative, isDefault);
					currentSection = line.TrimStart('[').TrimEnd(']');
					currentName = null;
					currentPath = null;
					isRelative = true;
					isDefault = false;
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
					// [ProfileN] Default=1 marks the legacy default profile.
					case "Default" when value == "1":
						isDefault = true;
						break;
				}

				// [Install<hash>] Default=<path> is the profile an installation opens without -P.
				if (
					key == "Default"
					&& currentSection?.StartsWith("Install", StringComparison.OrdinalIgnoreCase) == true
				)
				{
					installDefaults.Add(NormalizeIniPath(value));
				}
			}

			FlushEntry(entries, currentSection, currentName, currentPath, isRelative, isDefault);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			logger?.LogDebug(ex, "Could not read profiles.ini at {Path}", iniPath);
		}

		return new ProfilesIni(entries, installDefaults);
	}

	private static void FlushEntry(
		List<ProfileEntry> entries,
		string? section,
		string? name,
		string? path,
		bool isRelative,
		bool isDefault
	)
	{
		if (
			section != null
			&& section.StartsWith("Profile", StringComparison.OrdinalIgnoreCase)
			&& name != null
			&& path != null
		)
		{
			entries.Add(new ProfileEntry(name, path, isRelative, isDefault));
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
	/// Finds the profiles last used by this Firefox installation by checking
	/// <c>compatibility.ini</c> in each profile for a matching <c>LastPlatformDir</c>.
	/// </summary>
	private static List<ProfileEntry> FindInstallationProfiles(
		string root,
		List<ProfileEntry> entries,
		string? browserExePath,
		ILogger? logger
	)
	{
		var matches = new List<ProfileEntry>();
		var installDir = ExtractInstallDirectory(browserExePath);

		if (installDir != null)
		{
			foreach (var entry in entries)
			{
				var fullPath = FullPath(root, entry);
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
					matches.Add(entry with { LastMajorVersion = ReadLastMajorVersion(compatPath) });
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					logger?.LogDebug(ex, "Could not read compatibility.ini at {Path}", compatPath);
				}
			}
		}

		return matches;
	}

	/// <summary>
	/// Reads the major version from <c>LastVersion=66.0.5_20190513100000/20190513100000</c>.
	/// </summary>
	private static int? ReadLastMajorVersion(string compatPath)
	{
		var version = File.ReadLines(compatPath)
			.Where(line => line.StartsWith("LastVersion=", StringComparison.OrdinalIgnoreCase))
			.Select(line => line["LastVersion=".Length..].Trim())
			.FirstOrDefault();
		if (version == null)
			return null;
		var end = version.IndexOfAny(['.', '_']);
		return int.TryParse(end < 0 ? version : version[..end], out var major) ? major : null;
	}

	/// <summary>
	/// Drops profiles last run before Firefox 67 when the installation has a dedicated profile.
	/// Firefox 67 created a new dedicated profile (usually <c>default-release</c>) and left the old
	/// <c>default</c> profile behind, still pointing at the same installation in <c>compatibility.ini</c>.
	/// Counting that leftover would switch single-profile users to the multi-profile list.
	/// </summary>
	private static List<ProfileEntry> DropLegacyProfiles(List<ProfileEntry> installed, ProfilesIni ini, ILogger? logger)
	{
		if (installed.Count < 2 || !installed.Any(p => IsInstallDefault(ini, p)))
			return installed;

		return
		[
			.. installed.Where(p =>
			{
				var legacy = !IsInstallDefault(ini, p) && p.LastMajorVersion < DedicatedProfilesVersion;
				if (legacy)
					logger?.LogDebug(
						"Ignoring Firefox profile {Name}: last used by Firefox {Version}, before dedicated profiles",
						p.Name,
						p.LastMajorVersion
					);
				return !legacy;
			}),
		];
	}

	private static bool IsInstallDefault(ProfilesIni ini, ProfileEntry profile) =>
		ini.InstallDefaults.Contains(NormalizeIniPath(profile.Path));

	/// <summary>
	/// Falls back to the first existing profile directory when no profile matches the installation.
	/// </summary>
	private static string? FindFallbackProfilePath(
		string root,
		List<ProfileEntry> entries,
		string? browserExePath,
		ILogger? logger
	)
	{
		var fallback = entries.Select(e => FullPath(root, e)).FirstOrDefault(Directory.Exists);

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

	/// <summary>
	/// Returns every profile of the installation, the default one first, each followed by its containers.
	/// </summary>
	private static List<BrowserProfile> DiscoverProfilesAndContainers(
		string root,
		ProfilesIni ini,
		List<ProfileEntry> installed,
		ILogger? logger
	)
	{
		// profiles.ini has one [Install<hash>] section per installation, and the hash cannot be mapped back to a
		// folder. When several of this installation's profiles are some installation's default, the one this
		// installation opens without -P is unknown, so every profile is launched with -P instead of guessing.
		var installDefaults = installed.Where(p => IsInstallDefault(ini, p)).ToList();
		var defaultProfile = installDefaults.Count switch
		{
			1 => installDefaults[0],
			0 => installed.FirstOrDefault(p => p.IsDefault) ?? installed[0],
			_ => null,
		};
		if (defaultProfile == null)
			logger?.LogDebug("Several Firefox profiles are installation defaults; launching each with -P");

		var result = new List<BrowserProfile>();
		foreach (var profile in installed.OrderBy(p => p == defaultProfile ? 0 : 1))
		{
			var isDefault = profile == defaultProfile;
			result.Add(new BrowserProfile($"profile:{profile.Name}", profile.Name, ProfileArgs(profile)));
			result.AddRange(
				ReadContainers(FullPath(root, profile), logger).Select(c => CreateContainer(c, profile, isDefault))
			);
			logger?.LogDebug("Discovered Firefox profile: {Name} (default={IsDefault})", profile.Name, isDefault);
		}
		return result;
	}

	private static string ProfileArgs(ProfileEntry profile) => "-P " + QuoteArgument(profile.Name);

	/// <summary>
	/// Quotes a command-line argument following the Windows argument parsing rules: backslashes before a quote
	/// are doubled and quotes are escaped, so a name such as <c>Work\</c> or <c>My "main"</c> stays one argument.
	/// </summary>
	public static string QuoteArgument(string value)
	{
		var builder = new System.Text.StringBuilder("\"");
		var backslashes = 0;
		foreach (var c in value)
		{
			if (c == '\\')
			{
				backslashes++;
				continue;
			}
			builder.Append('\\', c == '"' ? backslashes * 2 + 1 : backslashes);
			backslashes = 0;
			builder.Append(c);
		}
		return builder.Append('\\', backslashes * 2).Append('"').ToString();
	}

	/// <summary>
	/// Creates the entry for a container. <paramref name="owner"/> is null when the installation has a single profile.
	/// </summary>
	private static BrowserProfile CreateContainer(
		ContainerIdentity container,
		ProfileEntry? owner,
		bool isDefaultProfile
	)
	{
		var inDefaultProfile = owner == null || isDefaultProfile;
		var id = inDefaultProfile ? $"container:{container.Name}" : $"profile:{owner!.Name}/container:{container.Name}";
		var name = owner == null ? container.Name : owner.Name + ProfileContainerSeparator + container.Name;

		// The container extension splits the link on '&' and creates any container it cannot find, so the
		// name is escaped like the URL: "R&D" must not turn into a new container called "R".
		return new BrowserProfile(
			id: id,
			name: name,
			commandArgs: inDefaultProfile ? null : ProfileArgs(owner!),
			urlTemplate: $"ext+container:name={Uri.EscapeDataString(container.Name)}&url={{url}}"
		)
		{
			IconColor = container.Color,
			ContainerIcon = container.Icon,
		};
	}

	private static List<ContainerIdentity> ReadContainers(string profilePath, ILogger? logger)
	{
		var containers = new List<ContainerIdentity>();
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

				containers.Add(
					new ContainerIdentity(
						name,
						string.IsNullOrEmpty(color) ? null : color,
						string.IsNullOrEmpty(icon) ? null : icon
					)
				);

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
