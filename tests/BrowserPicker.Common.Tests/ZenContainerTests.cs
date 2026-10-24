using System;
using System.IO;
using BrowserPicker.Windows.ProfileDiscovery;

namespace BrowserPicker.Common.Tests;

public class ZenContainerTests
{
	[Theory]
	[InlineData("Zen Browser", null)]
	[InlineData("Custom Zen", "\"C:\\Program Files\\Zen Browser\\zen.exe\"")]
	[InlineData("Firefox-1234567890ABCDEF", "C:\\Program Files\\Zen Browser\\ZEN.EXE")]
	public void RecognizesZenAndUsesItsOwnProfileDirectory(string name, string? command)
	{
		var known = WellKnownBrowsers.Lookup(name, command);
		Assert.Same(Zen.Instance, known);
		Assert.Equal(ProfileType.Firefox, known!.ProfileType);
		Assert.Equal("zen", known.UserDataPath);
	}

	[Fact]
	public void DiscoversZenContainersUsingExistingFirefoxDiscovery()
	{
		var root = Path.Combine(Path.GetTempPath(), "BrowserPicker-Zen-" + Guid.NewGuid());
		var other = Path.Combine(root, "Profiles", "other");
		var active = Path.Combine(root, "Profiles", "active");
		Directory.CreateDirectory(other);
		Directory.CreateDirectory(active);
		try
		{
			File.WriteAllText(
				Path.Combine(root, "profiles.ini"),
				"[Profile0]\nName=Other\nIsRelative=1\nPath=Profiles/other\n"
					+ "[Profile1]\nName=Active\nIsRelative=1\nPath=Profiles/active\n"
			);
			File.WriteAllText(Path.Combine(other, "compatibility.ini"), "LastPlatformDir=C:\\Other Zen\n");
			File.WriteAllText(
				Path.Combine(active, "compatibility.ini"),
				"LastPlatformDir=C:\\Program Files\\Zen Browser\n"
			);
			File.WriteAllText(
				Path.Combine(other, "containers.json"),
				"""{"identities":[{"name":"Wrong profile","public":true}]}"""
			);
			File.WriteAllText(
				Path.Combine(active, "containers.json"),
				"""{"identities":[{"name":"Video","public":true,"color":"green","icon":"briefcase"},{"name":"Internal","public":false}]}"""
			);

			var containers = FirefoxProfileDiscovery.Discover(root, "\"C:\\Program Files\\Zen Browser\\zen.exe\"");
			var container = Assert.Single(containers);
			Assert.Equal("Video", container.Name);
			Assert.Equal("green", container.IconColor);
			Assert.Equal("briefcase", container.ContainerIcon);
			Assert.Equal(
				"ext+container:name=Video&url=https%3A%2F%2Fexample.com%2F%3Fa%3D1%26b%3D2",
				container.TransformUrl("https://example.com/?a=1&b=2")
			);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void FirefoxStillUsesFirefoxProfiles()
	{
		Assert.Same(Firefox.Instance, WellKnownBrowsers.Lookup("Mozilla Firefox", "firefox.exe"));
		Assert.Equal(@"Mozilla\Firefox", Firefox.Instance.UserDataPath);
	}
}
