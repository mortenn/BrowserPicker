namespace BrowserPicker.Common.Tests;

public class LaunchArgumentsTests
{
	private const string Url = "https://example.com/";

	[Theory]
	[InlineData(null, "\"https://example.com/\"")]
	[InlineData("", "\"https://example.com/\"")]
	[InlineData("--incognito ", "--incognito \"https://example.com/\"")]
	[InlineData("--incognito", "--incognito \"https://example.com/\"")]
	[InlineData("-private-window", "-private-window \"https://example.com/\"")]
	[InlineData("--app=", "--app=\"https://example.com/\"")]
	public void SeparatesPrivacyArgumentsFromTheUrl(string? privacyArgs, string expected)
	{
		Assert.Equal(expected, LaunchArguments.Build(null, null, privacyArgs, Url));
	}

	[Fact]
	public void PutsBrowserThenProfileThenPrivacyArgumentsBeforeTheUrl()
	{
		var args = LaunchArguments.Build("--new-window", "--profile-directory=\"Profile 1\"", "--incognito", Url);

		Assert.Equal("--new-window --profile-directory=\"Profile 1\" --incognito \"https://example.com/\"", args);
	}

	[Fact]
	public void SkipsEmptyBrowserAndProfileArguments()
	{
		Assert.Equal("\"https://example.com/\"", LaunchArguments.Build("", "", null, Url));
	}
}
