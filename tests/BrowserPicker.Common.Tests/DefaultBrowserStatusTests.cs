using System.Globalization;
using BrowserPicker.Windows;

namespace BrowserPicker.Common.Tests;

public class DefaultBrowserStatusTests
{
	[Theory]
	[InlineData("BrowserPicker", true)]
	[InlineData("BrowserPickerPortable", true)]
	[InlineData("browserpicker", true)]
	[InlineData("ChromeHTML", false)]
	[InlineData("BrowserPickerOld", false)]
	[InlineData(null, false)]
	public void RecognizesBrowserPickerProgIds(string? progId, bool expected)
	{
		Assert.Equal(expected, DefaultBrowserStatus.IsBrowserPicker(progId));
	}

	[Theory]
	[InlineData("BrowserPicker", "BrowserPicker")]
	[InlineData("BrowserPickerPortable", "BrowserPickerPortable")]
	public void IsDefaultWhenBothSchemesUseBrowserPicker(string http, string https)
	{
		UseEnglish();
		var status = new DefaultBrowserStatus(http, https);

		Assert.True(status.IsDefault);
		Assert.Equal("Browser Picker opens web links.", status.Description);
	}

	[Fact]
	public void NamesTheSchemeThatStillOpensAnotherApp()
	{
		UseEnglish();
		var status = new DefaultBrowserStatus("MSEdgeHTM", "BrowserPicker");

		Assert.False(status.IsDefault);
		Assert.Contains("opens https links, but http links open with another app (MSEdgeHTM)", status.Description);
	}

	[Fact]
	public void NamesTheOtherAppWhenBrowserPickerIsNotTheDefault()
	{
		UseEnglish();
		var status = new DefaultBrowserStatus("ChromeHTML", "ChromeHTML");

		Assert.False(status.IsDefault);
		Assert.StartsWith("Web links open with another app (ChromeHTML), not Browser Picker.", status.Description);
	}

	[Fact]
	public void ExplainsWhenWindowsHasNoChoice()
	{
		UseEnglish();
		var status = new DefaultBrowserStatus(null, null);

		Assert.False(status.IsDefault);
		Assert.StartsWith("Windows has no default app for web links.", status.Description);
	}

	[Theory]
	[InlineData("BrowserPickerPortable", null, "ms-settings:defaultapps?registeredAppUser=BrowserPickerPortable")]
	[InlineData(null, "BrowserPicker", "ms-settings:defaultapps?registeredAppMachine=BrowserPicker")]
	[InlineData(
		"BrowserPickerPortable",
		"BrowserPicker",
		"ms-settings:defaultapps?registeredAppUser=BrowserPickerPortable"
	)]
	[InlineData(null, null, "ms-settings:defaultapps")]
	[InlineData(null, "Browser Picker", "ms-settings:defaultapps?registeredAppMachine=Browser%20Picker")]
	public void LinksToBrowserPickersPageInDefaultApps(string? user, string? machine, string expected)
	{
		Assert.Equal(expected, DefaultBrowserStatus.GetDefaultAppsUri(user, machine));
	}

	// The descriptions are translated; these tests check the English text whatever the Windows display language.
	private static void UseEnglish() => CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
}
