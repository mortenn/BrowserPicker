namespace BrowserPicker.Common.Tests;

public class BrowserDetectionUpdateTests
{
	private static BrowserModel Configured() =>
		new("Brave", @"C:\old\brave.exe", "\"C:\\old\\brave.exe\"")
		{
			CommandArgs = "--new-window",
			PrivacyArgs = "--incognito ",
		};

	[Fact]
	public void KeepsArgumentsTheUserEnteredWhenDetectionHasNone()
	{
		var browser = Configured();
		var detected = new BrowserModel("Brave", @"C:\new\brave.exe", "\"C:\\new\\brave.exe\"");

		browser.UpdateFromDetection(detected);

		Assert.Equal("\"C:\\new\\brave.exe\"", browser.Command);
		Assert.Equal(@"C:\new\brave.exe", browser.IconPath);
		Assert.Equal("--new-window", browser.CommandArgs);
		Assert.Equal("--incognito ", browser.PrivacyArgs);
	}

	[Fact]
	public void AppliesPrivacyArgumentsOfWellKnownBrowsers()
	{
		var browser = Configured();
		browser.PrivacyArgs = null;
		var detected = new BrowserModel(Brave.Instance, @"C:\new\brave.exe", "\"C:\\new\\brave.exe\"");

		browser.UpdateFromDetection(detected);

		Assert.Equal("--incognito ", browser.PrivacyArgs);
		Assert.Equal("--new-window", browser.CommandArgs);
	}

	[Fact]
	public void ManualOverrideKeepsEverything()
	{
		var browser = Configured();
		browser.ManualOverride = true;
		var detected = new BrowserModel(Brave.Instance, @"C:\new\brave.exe", "\"C:\\new\\brave.exe\"");

		browser.UpdateFromDetection(detected);

		Assert.Equal("\"C:\\old\\brave.exe\"", browser.Command);
		Assert.Equal(@"C:\old\brave.exe", browser.IconPath);
		Assert.Equal("--new-window", browser.CommandArgs);
	}

	[Fact]
	public void RaisesNoChangeWhenDetectionFoundTheSameValues()
	{
		var browser = Configured();
		var changes = new List<string?>();
		browser.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
		var detected = new BrowserModel(Brave.Instance, @"C:\old\brave.exe", "\"C:\\old\\brave.exe\"");

		browser.UpdateFromDetection(detected);

		Assert.Empty(changes);
	}
}
