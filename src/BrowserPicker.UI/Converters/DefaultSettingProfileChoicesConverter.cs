using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using BrowserPicker.Common;
using BrowserPicker.UI.ViewModels;

namespace BrowserPicker.UI.Converters;

/// <summary>
/// Produces the profile choices for a default rule row: a "(none)" entry followed by the enabled
/// profiles of the browser referenced by the rule. Values: [0] browser id or name, [1] browser choices.
/// </summary>
public sealed class DefaultSettingProfileChoicesConverter : IMultiValueConverter
{
	/// <summary>Placeholder entry meaning "no specific profile"; its empty id is stored as null on the rule.</summary>
	public static readonly BrowserProfile NoProfile = new(string.Empty, "(none)", null);

	public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
	{
		var result = new List<BrowserProfile> { NoProfile };
		if (
			values.Length < 2
			|| values[0] is not string browserKey
			|| values[1] is not IEnumerable<BrowserViewModel> choices
		)
		{
			return result;
		}

		var browser = choices.FirstOrDefault(c => c.Model.Id == browserKey || c.Model.Name == browserKey);
		if (browser != null)
		{
			result.AddRange(browser.Model.Profiles.Where(p => !p.Disabled));
		}
		return result;
	}

	public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
