using System;
using System.Globalization;
using System.Windows.Data;
using BrowserPicker.UI.SecurityProfiles;

namespace BrowserPicker.UI.Converters;

public sealed class SecurityProfileSelectionConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
	{
		return values.Length >= 2
			&& values[0] is ISecurityProfile selected
			&& values[1] is ISecurityProfile candidate
			&& string.Equals(selected.Id, candidate.Id, StringComparison.Ordinal);
	}

	public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
	{
		return [Binding.DoNothing, Binding.DoNothing];
	}
}
