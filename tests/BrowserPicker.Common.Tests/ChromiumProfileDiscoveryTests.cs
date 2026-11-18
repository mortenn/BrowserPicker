using System;
using System.IO;
using System.Linq;
using BrowserPicker.Windows.ProfileDiscovery;

namespace BrowserPicker.Common.Tests;

/// <summary>
/// Regression tests for issue #347: Chromium profile names must come from <c>Local State</c>,
/// which is where the browser stores renamed / account-derived profile names.
/// </summary>
public sealed class ChromiumProfileDiscoveryTests : IDisposable
{
	private readonly string root = Path.Combine(Path.GetTempPath(), "BrowserPicker-Chromium-" + Guid.NewGuid());

	public ChromiumProfileDiscoveryTests()
	{
		Directory.CreateDirectory(root);
	}

	public void Dispose()
	{
		Directory.Delete(root, recursive: true);
	}

	private void CreateProfileDirectory(string dirName, string? preferencesName = null)
	{
		var dir = Path.Combine(root, dirName);
		Directory.CreateDirectory(dir);
		if (preferencesName != null)
		{
			File.WriteAllText(Path.Combine(dir, "Preferences"), $$$"""{"profile":{"name":"{{{preferencesName}}}"}}""");
		}
	}

	private void WriteLocalState(string json)
	{
		File.WriteAllText(Path.Combine(root, "Local State"), json);
	}

	[Fact]
	public void UsesRenamedProfileNameFromLocalState()
	{
		CreateProfileDirectory("Default", "Person 1");
		CreateProfileDirectory("Profile 1", "Profile 1");
		WriteLocalState(
			"""
			{"profile":{"info_cache":{
				"Default":{"name":"Personal","is_using_default_name":false},
				"Profile 1":{"name":"Work","is_using_default_name":false}
			}}}
			"""
		);

		var profiles = ChromiumProfileDiscovery.Discover(root);

		Assert.Equal(["Default", "Profile 1"], profiles.Select(p => p.Id));
		Assert.Equal(["Personal", "Work"], profiles.Select(p => p.Name));
		Assert.Equal("--profile-directory=\"Profile 1\"", profiles[1].CommandArgs);
	}

	[Fact]
	public void UsesAccountNameWhenProfileHasDefaultName()
	{
		CreateProfileDirectory("Default");
		CreateProfileDirectory("Profile 1");
		CreateProfileDirectory("Profile 2");
		WriteLocalState(
			"""
			{"profile":{"info_cache":{
				"Default":{"name":"Person 1","is_using_default_name":true,"gaia_given_name":"Alex","gaia_name":"Alex Example"},
				"Profile 1":{"name":"Profile 1","is_using_default_name":true,"gaia_name":"Sam Example"},
				"Profile 2":{"name":"Custom","is_using_default_name":false,"gaia_given_name":"Ignored"}
			}}}
			"""
		);

		var profiles = ChromiumProfileDiscovery.Discover(root);

		Assert.Equal(["Alex", "Sam Example", "Custom"], profiles.Select(p => p.Name));
	}

	[Fact]
	public void FallsBackToLocalNameWhenDefaultNamedProfileIsNotSignedIn()
	{
		CreateProfileDirectory("Default");
		CreateProfileDirectory("Profile 1");
		WriteLocalState(
			"""
			{"profile":{"info_cache":{
				"Default":{"name":"Person 1","is_using_default_name":true},
				"Profile 1":{"is_using_default_name":true}
			}}}
			"""
		);

		var profiles = ChromiumProfileDiscovery.Discover(root);

		Assert.Equal(["Person 1", "Profile 1"], profiles.Select(p => p.Name));
	}

	[Fact]
	public void FollowsProfilesOrderAndAppendsUnorderedProfiles()
	{
		CreateProfileDirectory("Default");
		CreateProfileDirectory("Profile 1");
		CreateProfileDirectory("Profile 2");
		WriteLocalState(
			"""
			{"profile":{
				"profiles_order":["Profile 1","Missing","Default"],
				"info_cache":{
					"Default":{"name":"A"},
					"Profile 1":{"name":"B"},
					"Profile 2":{"name":"C"}
				}
			}}
			"""
		);

		var profiles = ChromiumProfileDiscovery.Discover(root);

		Assert.Equal(["Profile 1", "Default", "Profile 2"], profiles.Select(p => p.Id));
	}

	[Fact]
	public void SkipsInternalAndDeletedProfiles()
	{
		CreateProfileDirectory("Default");
		CreateProfileDirectory("Profile 1");
		CreateProfileDirectory("Guest Profile", "Guest");
		CreateProfileDirectory("System Profile", "Person 1");
		WriteLocalState(
			"""
			{"profile":{"info_cache":{
				"Default":{"name":"Personal"},
				"Profile 1":{"name":"Work"},
				"Profile 9":{"name":"Deleted from disk"},
				"Guest Profile":{"name":"Guest"}
			}}}
			"""
		);

		var profiles = ChromiumProfileDiscovery.Discover(root);

		Assert.Equal(["Default", "Profile 1"], profiles.Select(p => p.Id));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("not json")]
	[InlineData("""{"profile":{}}""")]
	public void FallsBackToPreferencesWhenLocalStateIsUnusable(string? localState)
	{
		CreateProfileDirectory("Default", "Personal");
		CreateProfileDirectory("Profile 1", "Work");
		CreateProfileDirectory("Guest Profile", "Guest");
		CreateProfileDirectory("System Profile", "Person 1");
		if (localState != null)
		{
			WriteLocalState(localState);
		}

		var profiles = ChromiumProfileDiscovery.Discover(root);

		Assert.Equal(["Personal", "Work"], profiles.Select(p => p.Name).Order());
	}

	[Fact]
	public void ReturnsNothingForSingleProfile()
	{
		CreateProfileDirectory("Default");
		CreateProfileDirectory("Guest Profile", "Guest");
		WriteLocalState("""{"profile":{"info_cache":{"Default":{"name":"Personal"}}}}""");

		Assert.Empty(ChromiumProfileDiscovery.Discover(root));
	}
}
