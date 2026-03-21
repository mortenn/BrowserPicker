using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace BrowserPicker.Windows.ProfileDiscovery;

/// <summary>
/// Discovers profiles for Chromium-based browsers (Chrome, Edge, etc.) by scanning the User Data directory.
/// Each subdirectory containing a Preferences file with a profile name is treated as a profile.
/// </summary>
public static class ChromiumProfileDiscovery
{
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
            userDataPath);

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

            profiles.Add(new BrowserProfile(
                id: dirName,
                name: displayName,
                commandArgs: $"--profile-directory=\"{dirName}\""));

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
            using var stream = File.OpenRead(preferencesPath);
            var root = JsonNode.Parse(stream);
            var name = root?["profile"]?["name"]?.GetValue<string>();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger?.LogDebug(ex, "Could not read profile name from {Path}", preferencesPath);
            return null;
        }
    }
}
