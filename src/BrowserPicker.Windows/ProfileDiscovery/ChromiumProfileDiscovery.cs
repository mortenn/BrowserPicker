using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BrowserPicker.Common;
using Microsoft.Extensions.Logging;

namespace BrowserPicker.Windows.ProfileDiscovery;

/// <summary>
/// Discovers profiles for Chromium-based browsers (Chrome, Edge, etc.) by reading the User Data directory.
/// </summary>
/// <remarks>
/// The authoritative list of profiles and their display names lives in the <c>Local State</c> file
/// (<c>profile.info_cache</c>); renaming a profile in the browser only updates that file.
/// The per-profile <c>Preferences</c> file is used as a fallback when <c>Local State</c> is unavailable.
/// </remarks>
public static class ChromiumProfileDiscovery
{
	/// <summary>
	/// Upper bound on the size of a Preferences / Local State file we will attempt to read.
	/// Real Chromium files are well under this; anything larger is
	/// assumed corrupt and is skipped to avoid excessive allocation / OutOfMemoryException.
	/// </summary>
	private const long MaxJsonFileSize = 32 * 1024 * 1024;

	/// <summary>
	/// Internal profile directories that are not user profiles and cannot be launched with
	/// <c>--profile-directory</c>.
	/// </summary>
	private static readonly HashSet<string> InternalProfileDirectories = new(StringComparer.OrdinalIgnoreCase)
	{
		"Guest Profile",
		"System Profile",
	};

	/// <summary>
	/// Discovers all profiles for a Chromium-based browser.
	/// </summary>
	/// <param name="userDataPath">
	/// Relative path under <c>%LocalAppData%</c> to the browser's User Data directory
	/// (e.g. <c>Google\Chrome\User Data</c>). An absolute path is used as-is.
	/// </param>
	/// <param name="logger">Optional logger for diagnostic output.</param>
	/// <returns>List of discovered profiles; empty if the path does not exist or has fewer than two profiles.</returns>
	public static List<BrowserProfile> Discover(string userDataPath, ILogger? logger = null)
	{
		var root = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			userDataPath
		);

		if (!Directory.Exists(root))
		{
			logger?.LogDebug("Chromium User Data directory not found: {Path}", root);
			return [];
		}

		var profiles = DiscoverFromLocalState(root, logger) ?? DiscoverFromPreferences(root, logger);

		if (profiles.Count > 1)
		{
			return profiles;
		}

		logger?.LogDebug("Only {Count} Chromium profile(s) found, no profile selection needed", profiles.Count);
		return [];
	}

	private static BrowserProfile CreateProfile(string dirName, string displayName, ILogger? logger)
	{
		logger?.LogDebug("Discovered Chromium profile: {Id} ({Name})", dirName, displayName);
		return new BrowserProfile(id: dirName, name: displayName, commandArgs: $"--profile-directory=\"{dirName}\"");
	}

	#region Local State

	/// <summary>
	/// Reads profiles from <c>Local State</c>. Returns null when the file is missing, unreadable,
	/// or lists no usable profiles, so the caller can fall back to scanning Preferences files.
	/// </summary>
	private static List<BrowserProfile>? DiscoverFromLocalState(string root, ILogger? logger)
	{
		var file = Path.Combine(root, "Local State");
		if (!File.Exists(file))
		{
			logger?.LogDebug("Chromium Local State not found: {Path}", file);
			return null;
		}

		try
		{
			var size = new FileInfo(file).Length;
			if (size > MaxJsonFileSize)
			{
				logger?.LogDebug(
					"Skipping Local State file larger than {Limit} bytes ({Size} bytes): {Path}",
					MaxJsonFileSize,
					size,
					file
				);
				return null;
			}

			using var document = JsonDocument.Parse(File.ReadAllBytes(file));
			if (
				document.RootElement.ValueKind != JsonValueKind.Object
				|| !document.RootElement.TryGetProperty("profile", out var profile)
				|| profile.ValueKind != JsonValueKind.Object
				|| !profile.TryGetProperty("info_cache", out var infoCache)
				|| infoCache.ValueKind != JsonValueKind.Object
			)
			{
				return null;
			}

			var profiles = new List<BrowserProfile>();
			foreach (var dirName in GetProfileOrder(profile, infoCache))
			{
				if (InternalProfileDirectories.Contains(dirName) || !Directory.Exists(Path.Combine(root, dirName)))
				{
					continue;
				}

				var displayName = ResolveDisplayName(infoCache.GetProperty(dirName)) ?? dirName;
				profiles.Add(CreateProfile(dirName, displayName, logger));
			}

			return profiles.Count > 0 ? profiles : null;
		}
		catch (Exception ex)
			when (ex is JsonException or IOException or UnauthorizedAccessException or OutOfMemoryException)
		{
			logger?.LogDebug(ex, "Could not read profiles from {Path}", file);
			return null;
		}
	}

	/// <summary>
	/// Returns the profile directory names in the order the browser shows them: entries from
	/// <c>profile.profiles_order</c> first, followed by any remaining <c>info_cache</c> entries.
	/// </summary>
	private static List<string> GetProfileOrder(JsonElement profile, JsonElement infoCache)
	{
		var known = infoCache.EnumerateObject().Select(p => p.Name).ToList();
		var ordered = new List<string>();

		if (profile.TryGetProperty("profiles_order", out var order) && order.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in order.EnumerateArray())
			{
				if (item.ValueKind != JsonValueKind.String)
				{
					continue;
				}

				var name = item.GetString();
				if (name != null && known.Contains(name) && !ordered.Contains(name))
				{
					ordered.Add(name);
				}
			}
		}

		ordered.AddRange(known.Where(name => !ordered.Contains(name)));
		return ordered;
	}

	/// <summary>
	/// Mirrors how Chromium picks the display name: a signed-in profile that still uses its
	/// default local name is shown with the account's name; otherwise the local name is used.
	/// </summary>
	private static string? ResolveDisplayName(JsonElement entry)
	{
		if (entry.ValueKind != JsonValueKind.Object)
		{
			return null;
		}

		var localName = GetString(entry, "name");
		var usingDefaultName =
			entry.TryGetProperty("is_using_default_name", out var isDefault)
			&& isDefault.ValueKind == JsonValueKind.True;

		if (usingDefaultName || localName == null)
		{
			var gaiaName = GetString(entry, "gaia_given_name") ?? GetString(entry, "gaia_name");
			if (gaiaName != null)
			{
				return gaiaName;
			}
		}

		return localName;
	}

	private static string? GetString(JsonElement element, string property)
	{
		return
			element.TryGetProperty(property, out var value)
			&& value.ValueKind == JsonValueKind.String
			&& !string.IsNullOrWhiteSpace(value.GetString())
			? value.GetString()
			: null;
	}

	#endregion

	#region Preferences fallback

	/// <summary>
	/// Fallback discovery: each subdirectory containing a Preferences file with a profile name is treated as a profile.
	/// </summary>
	private static List<BrowserProfile> DiscoverFromPreferences(string root, ILogger? logger)
	{
		var profiles = new List<BrowserProfile>();
		foreach (var dir in Directory.GetDirectories(root))
		{
			var dirName = Path.GetFileName(dir);
			if (InternalProfileDirectories.Contains(dirName))
			{
				continue;
			}

			var file = Path.Combine(dir, "Preferences");
			if (!File.Exists(file))
			{
				continue;
			}

			var displayName = ReadProfileName(file, logger);
			if (displayName == null)
			{
				continue;
			}

			profiles.Add(CreateProfile(dirName, displayName, logger));
		}

		return profiles;
	}

	private static string? ReadProfileName(string preferencesPath, ILogger? logger)
	{
		try
		{
			var size = new FileInfo(preferencesPath).Length;
			if (size > MaxJsonFileSize)
			{
				logger?.LogDebug(
					"Skipping Preferences file larger than {Limit} bytes ({Size} bytes): {Path}",
					MaxJsonFileSize,
					size,
					preferencesPath
				);
				return null;
			}

			var bytes = File.ReadAllBytes(preferencesPath);
			var name = ExtractProfileName(bytes);
			return string.IsNullOrWhiteSpace(name) ? null : name;
		}
		catch (Exception ex)
			when (ex is JsonException or IOException or UnauthorizedAccessException or OutOfMemoryException)
		{
			logger?.LogDebug(ex, "Could not read profile name from {Path}", preferencesPath);
			return null;
		}
	}

	/// <summary>
	/// Streams the JSON to read only <c>profile.name</c> without materializing the whole document,
	/// keeping allocations bounded for large Preferences files.
	/// </summary>
	private static string? ExtractProfileName(ReadOnlySpan<byte> utf8Json)
	{
		var reader = new Utf8JsonReader(utf8Json);

		// Find the top-level "profile" object.
		if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
		{
			return null;
		}

		var depth = reader.CurrentDepth;
		while (reader.Read())
		{
			if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != depth + 1)
			{
				continue;
			}

			if (!reader.ValueTextEquals("profile"))
			{
				reader.Read();
				reader.Skip();
				continue;
			}

			reader.Read();
			return reader.TokenType == JsonTokenType.StartObject ? ReadNameProperty(ref reader) : null;
		}

		return null;
	}

	/// <summary>
	/// Reads the <c>name</c> string from the current object the reader is positioned on.
	/// </summary>
	private static string? ReadNameProperty(ref Utf8JsonReader reader)
	{
		var depth = reader.CurrentDepth;
		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == depth)
			{
				break;
			}

			if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != depth + 1)
			{
				continue;
			}

			if (reader.ValueTextEquals("name"))
			{
				return reader.Read() && reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
			}

			reader.Read();
			reader.Skip();
		}

		return null;
	}

	#endregion
}
