using System.IO;
using AwesomeAssertions;
using BrowserPicker.Windows.ProfileDiscovery;

namespace BrowserPicker.Common.Tests;

public sealed class FirefoxProfileDiscoveryTests : IDisposable
{
	private const string Firefox = "\"C:\\Program Files\\Mozilla Firefox\\firefox.exe\"";
	private const string InstallDir = "C:\\Program Files\\Mozilla Firefox";

	private readonly string root = Path.Combine(Path.GetTempPath(), "BrowserPicker-Firefox-" + Guid.NewGuid());

	public void Dispose()
	{
		if (Directory.Exists(root))
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void EscapesContainerNamesInLinks()
	{
		WriteProfilesIni("[Profile0]\nName=default-release\nIsRelative=1\nPath=Profiles/main\n");
		AddProfile("main", InstallDir, "R&D Team");

		var container = FirefoxProfileDiscovery.Discover(root, Firefox).Should().ContainSingle().Subject;

		container.Id.Should().Be("container:R&D Team");
		container.Name.Should().Be("R&D Team");
		container.CommandArgs.Should().BeNull();
		container
			.TransformUrl("https://example.com/?a=1&b=2")
			.Should()
			.Be("ext+container:name=R%26D%20Team&url=https%3A%2F%2Fexample.com%2F%3Fa%3D1%26b%3D2");
	}

	[Fact]
	public void ListsEachProfileWithItsContainersWhenTheInstallationHasSeveral()
	{
		WriteProfilesIni(
			"[Install0123456789ABCDEF]\nDefault=Profiles/work\nLocked=1\n\n"
				+ "[Profile0]\nName=Personal\nIsRelative=1\nPath=Profiles/personal\nDefault=1\n\n"
				+ "[Profile1]\nName=Work\nIsRelative=1\nPath=Profiles/work\n\n"
				+ "[Profile2]\nName=Nightly\nIsRelative=1\nPath=Profiles/nightly\n"
		);
		AddProfile("personal", InstallDir, "Shopping");
		AddProfile("work", InstallDir, "Client");
		AddProfile("nightly", "C:\\Program Files\\Firefox Nightly", "Testing");

		var profiles = FirefoxProfileDiscovery.Discover(root, Firefox);

		var separator = FirefoxProfileDiscovery.ProfileContainerSeparator;
		profiles
			.Select(p => (p.Id, p.Name, p.CommandArgs))
			.Should()
			.Equal(
				("profile:Work", "Work", "-P \"Work\""),
				// Containers of the default profile keep their id and open without -P, so existing rules still work.
				("container:Client", $"Work{separator}Client", null),
				("profile:Personal", "Personal", "-P \"Personal\""),
				("profile:Personal/container:Shopping", $"Personal{separator}Shopping", "-P \"Personal\"")
			);
		profiles[0].UrlTemplate.Should().BeNull();
		profiles[3]
			.TransformUrl("https://example.com/")
			.Should()
			.Be("ext+container:name=Shopping&url=https%3A%2F%2Fexample.com%2F");
	}

	[Fact]
	public void UsesTheLegacyDefaultFlagWhenNoInstallSectionNamesTheDefault()
	{
		WriteProfilesIni(
			"[Profile0]\nName=Work\nIsRelative=1\nPath=Profiles/work\n\n"
				+ "[Profile1]\nName=Personal\nIsRelative=1\nPath=Profiles/personal\nDefault=1\n"
		);
		AddProfile("work", InstallDir);
		AddProfile("personal", InstallDir);

		var profiles = FirefoxProfileDiscovery.Discover(root, Firefox);

		profiles.Select(p => p.Id).Should().Equal("profile:Personal", "profile:Work");
	}

	private void WriteProfilesIni(string content)
	{
		Directory.CreateDirectory(root);
		File.WriteAllText(Path.Combine(root, "profiles.ini"), content);
	}

	private void AddProfile(string folder, string lastPlatformDir, params string[] containers)
	{
		var path = Path.Combine(root, "Profiles", folder);
		Directory.CreateDirectory(path);
		File.WriteAllText(Path.Combine(path, "compatibility.ini"), $"LastPlatformDir={lastPlatformDir}\n");
		var identities = string.Join(
			",",
			containers.Select(c => $$"""{"name":"{{c}}","public":true,"color":"blue","icon":"circle"}""")
		);
		File.WriteAllText(Path.Combine(path, "containers.json"), $$"""{"identities":[{{identities}}]}""");
	}
}
