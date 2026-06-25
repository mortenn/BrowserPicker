using System.Text.Json;
using AwesomeAssertions;

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
}
