using System;
using System.IO;
using System.Text.Json;
using AwesomeAssertions;
using BrowserPicker.Windows;

namespace BrowserPicker.Common.Tests;

/// <summary>
/// Regression tests for issue #304: browser profiles must survive a JSON round-trip so the picker
/// shows them on every launch (notably in flat / "separate entries" display mode).
/// </summary>
public class BrowserProfileSerializationTests
{
	[Fact]
	public void BrowserModelProfilesSurviveJsonRoundTrip()
	{
		var original = new BrowserModel("Firefox", null, "firefox.exe") { ContainersEnabled = true };
		original.Profiles.Add(new BrowserProfile("container:Work", "Work", null, "ext+container:name=Work&url={url}"));
		original.Profiles.Add(new BrowserProfile("container:Personal", "Personal", null));

		var json = JsonSerializer.Serialize(original);
		var restored = JsonSerializer.Deserialize<BrowserModel>(json);

		restored.Should().NotBeNull();
		restored!.Profiles.Should().HaveCount(2);
		restored.Profiles[0].Id.Should().Be("container:Work");
		restored.Profiles[0].Name.Should().Be("Work");
		restored.Profiles[0].UrlTemplate.Should().Be("ext+container:name=Work&url={url}");
		restored.Profiles[1].Id.Should().Be("container:Personal");
	}

	[Fact]
	public void HiddenProfileFlagSurvivesJsonRoundTrip()
	{
		var original = new BrowserModel("Chrome", null, "chrome.exe");
		original.Profiles.Add(new BrowserProfile("Default", "Personal", "--profile-directory=Default"));
		original.Profiles.Add(
			new BrowserProfile("Profile 1", "Junk", "--profile-directory=Profile 1") { Disabled = true }
		);

		var json = JsonSerializer.Serialize(original);
		var restored = JsonSerializer.Deserialize<BrowserModel>(json);

		restored!.Profiles.Should().HaveCount(2);
		restored.Profiles[0].Disabled.Should().BeFalse();
		restored.Profiles[1].Disabled.Should().BeTrue();
	}

	[Fact]
	public void ProfilesExpandedStateSurvivesJsonRoundTrip()
	{
		var original = new BrowserModel("Firefox", null, "firefox.exe") { ProfilesExpanded = true };

		var json = JsonSerializer.Serialize(original);
		var restored = JsonSerializer.Deserialize<BrowserModel>(json);

		restored!.ProfilesExpanded.Should().BeTrue();
	}

	[Fact]
	public void FailedSettingsLoadDoesNotWriteEmptyDefaultsBackToDisk()
	{
		var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		var browserPickerDir = Path.Combine(localAppData, "BrowserPicker");
		Directory.CreateDirectory(browserPickerDir);
		var settingsPath = Path.Combine(browserPickerDir, "settings.json");

		var originalJson = """
			{
			  "$schema": "https://raw.githubusercontent.com/mortenn/BrowserPicker/main/schemas/browserpicker-settings.schema.json",
			  "BrowserList": [
			    {
			      "Name": "Firefox",
			      "Id": "firefox",
			      "Executable": "firefox.exe",
			      "Profiles": []
			    }
			  ],
			  "Defaults": [
			    {
			      "Type": "Default",
			      "Pattern": "",
			      "Browser": "firefox"
			    }
			  ]
			}
			""";
		File.WriteAllText(settingsPath, originalJson);

		using var lockedFile = new FileStream(settingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
		var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<JsonAppSettings>();
		var settings = new JsonAppSettings(logger);

		lockedFile.Dispose();
		settings.AlwaysPrompt = true;

		var currentJson = File.ReadAllText(settingsPath);
		currentJson.Should().Contain("firefox");
		currentJson.Should().NotContain("\"Defaults\": []");
		settings.Should().NotBeNull();
	}
}
