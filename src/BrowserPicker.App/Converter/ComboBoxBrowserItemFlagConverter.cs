using System;
using System.Globalization;
using System.Windows.Data;
using BrowserPicker.ViewModel;

namespace BrowserPicker.Converter;

/// <summary>
/// Reads <see cref="BrowserViewModel"/> flags from a <see cref="System.Windows.Controls.ComboBoxItem"/>'s <c>Content</c> for style triggers (avoids <c>Content.Model.*</c> paths that XAML analyzers treat as <c>object</c>).
/// </summary>
public sealed class ComboBoxBrowserItemFlagConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not BrowserViewModel vm || parameter is not string flag)
			return false;

		return flag switch
		{
			"Disabled" => vm.Model.Disabled,
			"Removed" => vm.Model.Removed,
			_ => false
		};
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
