namespace BrowserPicker.Common.Tests;

public class FolderMatchTests
{
	private const string Folder = @"C:\Program Files\Palo Alto Networks\GlobalProtect";

	[Theory]
	[InlineData(@"C:\Program Files\Palo Alto Networks\GlobalProtect\welcome.html")]
	[InlineData(@"c:\program files\palo alto networks\globalprotect\welcome.html")]
	[InlineData(@"C:\Program Files\Palo Alto Networks\GlobalProtect\pages\done.html")]
	[InlineData("file:///C:/Program%20Files/Palo%20Alto%20Networks/GlobalProtect/welcome.html")]
	public void MatchesFilesInTheFolderAndItsSubfolders(string url)
	{
		var rule = new DefaultSetting(MatchType.Folder, Folder, "Edge");

		Assert.Equal(Folder.Length + 1, rule.MatchLength(new Uri(url)));
	}

	[Theory]
	[InlineData(@"C:\Program Files\Palo Alto Networks\GlobalProtect2\welcome.html")]
	[InlineData(@"C:\Program Files\Palo Alto Networks\welcome.html")]
	[InlineData("https://globalprotect.example.com/C:/Program Files/Palo Alto Networks/GlobalProtect/x.html")]
	public void DoesNotMatchOtherFoldersOrWebUrls(string url)
	{
		var rule = new DefaultSetting(MatchType.Folder, Folder, "Edge");

		Assert.Equal(0, rule.MatchLength(new Uri(url)));
	}

	[Theory]
	[InlineData(@"C:\Program Files\Palo Alto Networks\GlobalProtect\")]
	[InlineData("C:/Program Files/Palo Alto Networks/GlobalProtect")]
	[InlineData("file:///C:/Program%20Files/Palo%20Alto%20Networks/GlobalProtect/")]
	public void AcceptsTheFolderAsAPathOrAFileUrl(string pattern)
	{
		var rule = new DefaultSetting(MatchType.Folder, pattern, "Edge");

		Assert.True(rule.MatchLength(new Uri(Folder + @"\welcome.html")) > 0);
	}

	[Fact]
	public void MatchesNetworkShares()
	{
		var rule = new DefaultSetting(MatchType.Folder, @"\\server\share\reports", "Edge");

		Assert.True(rule.MatchLength(new Uri(@"\\server\share\reports\today.html")) > 0);
		Assert.True(rule.MatchLength(new Uri("file://server/share/reports/today.html")) > 0);
		Assert.Equal(0, rule.MatchLength(new Uri(@"\\server\share\other\today.html")));
	}

	[Theory]
	[InlineData("reports")]
	[InlineData(@"..\reports")]
	[InlineData("file://")]
	[InlineData(" ")]
	public void IgnoresPatternsThatAreNotAbsoluteFolders(string pattern)
	{
		var rule = new DefaultSetting(MatchType.Folder, pattern, "Edge");

		Assert.Equal(0, rule.MatchLength(new Uri(@"C:\reports\today.html")));
	}

	[Fact]
	public void LongerFolderWinsOverItsParent()
	{
		var url = new Uri(@"C:\Data\Reports\today.html");

		var parent = new DefaultSetting(MatchType.Folder, @"C:\Data", "Edge").MatchLength(url);
		var child = new DefaultSetting(MatchType.Folder, @"C:\Data\Reports", "Firefox").MatchLength(url);

		Assert.True(child > parent);
	}

	[Theory]
	[InlineData(@"C:\Program Files\GlobalProtect\welcome.html", @"C:\Program Files\GlobalProtect\")]
	[InlineData("file:///C:/Program%20Files/GlobalProtect/welcome.html", @"C:\Program Files\GlobalProtect\")]
	[InlineData(@"\\server\share\today.html", @"\\server\share\")]
	[InlineData("https://example.com/folder/page.html", null)]
	public void FindsTheFolderOfAFileUrl(string url, string? expected)
	{
		Assert.Equal(expected, FileFolder.Of(new Uri(url)));
	}

	[Fact]
	public void SavesAndLoadsTheFolderType()
	{
		var json = System.Text.Json.JsonSerializer.Serialize(new DefaultSetting(MatchType.Folder, Folder, "Edge"));
		var loaded = System.Text.Json.JsonSerializer.Deserialize<DefaultSetting>(json)!;

		Assert.Contains("\"Folder\"", json);
		Assert.Equal(MatchType.Folder, loaded.Type);
		Assert.Equal(Folder, loaded.Pattern);
	}
}
