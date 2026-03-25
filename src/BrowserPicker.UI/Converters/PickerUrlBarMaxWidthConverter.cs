using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BrowserPicker.UI.Converters;

/// <summary>
/// Caps the picker URL bar width during <see cref="SizeToContent"/> layout so a long URL does not force the
/// window to the full text width. Once the window has an <see cref="FrameworkElement.ActualWidth"/>, the bar
/// tracks it (still limited to the work area).
/// </summary>
public sealed class PickerUrlBarMaxWidthConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		const double subtractFromWindow = 36;
		var workAreaCap = Math.Max(280, SystemParameters.WorkArea.Width - 24);
		const double initialMeasureCap = 720;

		if (value is double w && w > 1 && !double.IsNaN(w) && !double.IsInfinity(w))
			return Math.Min(w - subtractFromWindow, workAreaCap);

		return Math.Min(initialMeasureCap, workAreaCap);
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
