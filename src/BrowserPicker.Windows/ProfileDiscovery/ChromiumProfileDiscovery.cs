using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BrowserPicker.Common;
using Microsoft.Extensions.Logging;

namespace BrowserPicker.Windows.ProfileDiscovery;

/// <summary>
/// Discovers profiles for Chromium-based browsers (Chrome, Edge, etc.) by scanning the User Data directory.
/// Each subdirectory containing a Preferences file with a profile name is treated as a profile.
/// </summary>
public static class ChromiumProfileDiscovery
{
	/// <summary>
	/// Upper bound on the size of a Preferences file we will attempt to read.
	/// Real Chromium Preferences files are well under this; anything larger is
	/// assumed corrupt and is skipped to avoid excessive allocation / OutOfMemoryException.
	/// </summary>
	private const long MaxPreferencesFileSize = 32 * 1024 * 1024;

	/// <summary>
	/// Discovers all profiles for a Chromium-based browser.
	/// </summary>
	/// <param name="userDataPath">
	/// Relative path under <c>%LocalAppData%</c> to the browser's User Data directory
	/// (e.g. <c>Google\Chrome\User Data</c>).
	/// </param>
	/// <param name="logger">Optional logger for diagnostic output.</param>
	/// <returns>List of discovered profiles; empty if the path does not exist or has no profiles.</returns>
	public static List<BrowserProfile> Discover(string userDataPath, ILogger? logger = null)
	{
		var profiles = new List<BrowserProfile>();
		var root = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			userDataPath
		);

		if (!Directory.Exists(root))
		{
			logger?.LogDebug("Chromium User Data directory not found: {Path}", root);
			return profiles;
		}

		foreach (var dir in Directory.GetDirectories(root))
		{
			var dirName = Path.GetFileName(dir);
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

			profiles.Add(
				new BrowserProfile(id: dirName, name: displayName, commandArgs: $"--profile-directory=\"{dirName}\"")
			);

			logger?.LogDebug("Discovered Chromium profile: {Id} ({Name})", dirName, displayName);
		}

		if (profiles.Count > 1)
		{
			return profiles;
		}

		logger?.LogDebug("Only {Count} Chromium profile(s) found, no profile selection needed", profiles.Count);
		return [];
	}

	private static string? ReadProfileName(string preferencesPath, ILogger? logger)
	{
		try
		{
			var size = new FileInfo(preferencesPath).Length;
			if (size > MaxPreferencesFileSize)
			{
				logger?.LogDebug(
					"Skipping Preferences file larger than {Limit} bytes ({Size} bytes): {Path}",
					MaxPreferencesFileSize,
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
}
