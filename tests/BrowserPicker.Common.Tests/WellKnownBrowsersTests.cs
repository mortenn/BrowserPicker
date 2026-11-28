namespace BrowserPicker.Common.Tests;

public class WellKnownBrowsersTests
{
	public static TheoryData<string, string?, IWellKnownBrowser> BraveChannels =>
		new()
		{
			{ "Brave", null, Brave.Instance },
			{
				"Renamed",
				"\"C:\\Program Files\\BraveSoftware\\Brave-Browser\\Application\\brave.exe\"",
				Brave.Instance
			},
			{
				"Renamed",
				"\"C:\\Users\\user\\AppData\\Local\\BraveSoftware\\Brave-Browser\\Application\\brave.exe\"",
				Brave.Instance
			},
			{ "Brave Beta", null, BraveBeta.Instance },
			{
				"Renamed",
				"\"C:\\Program Files\\BraveSoftware\\Brave-Browser-Beta\\Application\\brave.exe\"",
				BraveBeta.Instance
			},
			{ "Brave Nightly", null, BraveNightly.Instance },
			{
				"Renamed",
				"\"C:\\Users\\user\\AppData\\Local\\BraveSoftware\\Brave-Browser-Nightly\\Application\\brave.exe\"",
				BraveNightly.Instance
			},
		};

	[Theory]
	[MemberData(nameof(BraveChannels))]
	public void RecognizesEachBraveChannelByNameOrInstallFolder(
		string name,
		string? command,
		IWellKnownBrowser expected
	)
	{
		var known = WellKnownBrowsers.Lookup(name, command);

		Assert.Same(expected, known);
		Assert.Equal(ProfileType.Chromium, known!.ProfileType);
		Assert.Equal("--incognito ", known.PrivacyArgs);
	}

	[Fact]
	public void EachBraveChannelReadsItsOwnProfiles()
	{
		Assert.Equal(@"BraveSoftware\Brave-Browser\User Data", Brave.Instance.UserDataPath);
		Assert.Equal(@"BraveSoftware\Brave-Browser-Beta\User Data", BraveBeta.Instance.UserDataPath);
		Assert.Equal(@"BraveSoftware\Brave-Browser-Nightly\User Data", BraveNightly.Instance.UserDataPath);
	}

	[Fact]
	public void UnknownBrowserIsNotMatched()
	{
		Assert.Null(
			WellKnownBrowsers.Lookup(
				"Mullvad Browser",
				"\"C:\\Program Files\\Mullvad Browser\\Browser\\mullvadbrowser.exe\""
			)
		);
	}
}
