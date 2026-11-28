using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using BrowserPicker.Common.Resources;

namespace BrowserPicker.Common.Tests;

/// <summary>
/// Keeps the translations in step with the English strings: a key added to Strings.resx must be
/// translated, and a translation must keep the same {0}-style placeholders so string.Format does not throw.
/// </summary>
public sealed partial class StringsTests
{
	public static TheoryData<string> Translations => ["zh-Hans"];

	[Theory]
	[MemberData(nameof(Translations))]
	public void TranslationCoversEveryString(string culture)
	{
		var english = ReadResourceSet(CultureInfo.InvariantCulture);
		var translated = ReadResourceSet(CultureInfo.GetCultureInfo(culture));

		translated.Keys.Should().BeEquivalentTo(english.Keys);
		translated.Values.Should().AllSatisfy(value => value.Should().NotBeNullOrWhiteSpace());
	}

	[Theory]
	[MemberData(nameof(Translations))]
	public void TranslationKeepsFormatPlaceholders(string culture)
	{
		var english = ReadResourceSet(CultureInfo.InvariantCulture);
		var translated = ReadResourceSet(CultureInfo.GetCultureInfo(culture));

		foreach (var (key, value) in english)
		{
			if (translated.TryGetValue(key, out var translation))
			{
				Placeholders(translation).Should().Equal(Placeholders(value), "placeholders in {0} must match", key);
			}
		}
	}

	[Theory]
	[InlineData("zh-CN")]
	[InlineData("zh-SG")]
	public void SimplifiedChineseRegionsFallBackToTheTranslation(string culture)
	{
		var expected = Strings.ResourceManager.GetString(
			nameof(Strings.Common_Cancel),
			CultureInfo.GetCultureInfo("zh-Hans")
		);

		Strings
			.ResourceManager.GetString(nameof(Strings.Common_Cancel), CultureInfo.GetCultureInfo(culture))
			.Should()
			.Be(expected)
			.And.NotBe(Strings.ResourceManager.GetString(nameof(Strings.Common_Cancel), CultureInfo.InvariantCulture));
	}

	private static Dictionary<string, string> ReadResourceSet(CultureInfo culture)
	{
		var set = Strings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
		set.Should().NotBeNull($"a resource set for '{culture.Name}' should be embedded");
		return set!.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
	}

	private static List<string> Placeholders(string value) =>
		PlaceholderPattern().Matches(value).Select(match => match.Value).Order(StringComparer.Ordinal).ToList();

	[GeneratedRegex(@"\{\d+(?:[,:][^}]*)?\}")]
	private static partial Regex PlaceholderPattern();
}
