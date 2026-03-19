using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BrowserPicker.Converter;

/// <summary>
/// Converts a Firefox container color name (e.g. "blue", "orange") to a <see cref="SolidColorBrush"/>.
/// Returns null for unknown or null input.
/// </summary>
public sealed class ContainerColorConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not string colorName || !ColorMap.TryGetValue(colorName, out var brush))
			return null;
		return brush;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;

	private static readonly FrozenDictionary<string, SolidColorBrush> ColorMap =
		new Dictionary<string, SolidColorBrush>
		{
			["blue"] = Frozen(0x37, 0xad, 0xff),
			["turquoise"] = Frozen(0x00, 0xc7, 0x9a),
			["green"] = Frozen(0x51, 0xcd, 0x00),
			["yellow"] = Frozen(0xff, 0xcb, 0x00),
			["orange"] = Frozen(0xff, 0x9f, 0x00),
			["red"] = Frozen(0xff, 0x61, 0x3d),
			["pink"] = Frozen(0xff, 0x4b, 0xda),
			["purple"] = Frozen(0xaf, 0x51, 0xf5),
		}.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

	private static SolidColorBrush Frozen(byte r, byte g, byte b)
	{
		var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
		brush.Freeze();
		return brush;
	}
}

/// <summary>
/// Converts a Firefox container icon name (e.g. "briefcase", "circle") to a WPF <see cref="Geometry"/>.
/// The geometries are designed for a 16×16 coordinate space.
/// Returns null for unknown or null input.
/// </summary>
public sealed class ContainerIconConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not string iconName || !IconMap.TryGetValue(iconName, out var geometry))
			return null;
		return geometry;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;

	private static readonly FrozenDictionary<string, Geometry> IconMap;

	static ContainerIconConverter()
	{
		var dict = new Dictionary<string, Geometry>(StringComparer.OrdinalIgnoreCase)
		{
			["circle"] = G("M 8,1 A 7,7 0 1 0 8,15 A 7,7 0 1 0 8,1 Z"),
			["briefcase"] = G("M 2,6 H 14 V 14 H 2 Z  M 5.5,6 V 3.5 C 5.5,2.7 6.2,2 7,2 H 9 C 9.8,2 10.5,2.7 10.5,3.5 V 6"),
			["dollar"] = G(
				"M 8,1 V 3  M 8,13 V 15  " +
				"M 5.5,10 C 5.5,11.7 6.7,13 8,13 C 9.5,13 11,12 11,10.5 C 11,8 5,8.5 5,5.5 C 5,4 6.5,3 8,3 C 9.5,3 10.5,4.3 10.5,6"),
			["cart"] = G(
				"M 1,2 L 3.5,2 L 5.5,10 H 12.5 L 14.5,4.5 H 4.5  " +
				"M 6.5,12.5 A 1.2,1.2 0 1 0 6.501,12.5 Z  " +
				"M 11.5,12.5 A 1.2,1.2 0 1 0 11.501,12.5 Z"),
			["fingerprint"] = G(
				"M 4,12 C 2.5,10.5 2,9.3 2,8 A 6,6 0 0 1 14,8 C 14,9.5 13.5,10.8 12.5,12  " +
				"M 5,8 A 3,3 0 0 1 11,8 C 11,10 10,12 9,13.5  " +
				"M 7.5,8 A 0.5,0.5 0 0 1 8.5,8 C 8.5,10 8,12 7.5,14"),
			["fence"] = G(
				"M 3,3 V 13  M 6.5,2 V 13  M 10,3 V 13  M 13.5,2 V 13  " +
				"M 1,6 H 15  M 1,10 H 15"),
			["gift"] = G(
				"M 2,7 H 14 V 14 H 2 Z  M 8,7 V 14  " +
				"M 1.5,5 H 14.5 V 7 H 1.5 Z  " +
				"M 8,5 C 7,4 5,2 4.5,2.5 C 4,3 5,4.5 8,5  " +
				"M 8,5 C 9,4 11,2 11.5,2.5 C 12,3 11,4.5 8,5"),
			["vacation"] = G(
				"M 8,5 V 14  M 4,14 H 12  " +
				"M 8,4 C 6,3.5 3,3.5 2,5.5  M 8,4 C 10,3.5 13,3.5 14,5.5  " +
				"M 8,6.5 C 6.5,6 4,6 3,7.5  M 8,6.5 C 9.5,6 12,6 13,7.5"),
			["food"] = G(
				"M 5,2 V 6 C 5,7.5 6,8.5 7,8.5 V 14  " +
				"M 9,2 V 6 C 9,7.5 8,8.5 7,8.5  M 7,2 V 5.5  " +
				"M 12,2 C 12,2 13,4 13,6 V 14  M 13,7 H 12"),
			["fruit"] = G(
				"M 8,3.5 C 8.5,2 9.5,1 10.5,1  " +
				"M 5,4.5 C 3.5,5 2,7 2,9.5 C 2,12.5 4,14.5 6.5,14.5 C 7.5,14.5 8,14 8,14 " +
				"C 8,14 8.5,14.5 9.5,14.5 C 12,14.5 14,12.5 14,9.5 C 14,7 12.5,5 11,4.5 " +
				"C 9.5,4 8.5,4.5 8,5 C 7.5,4.5 6.5,4 5,4.5 Z"),
			["pet"] = G(
				"M 4,4 A 1.3,1.3 0 1 1 4.01,4 Z  " +
				"M 7,2.5 A 1.3,1.3 0 1 1 7.01,2.5 Z  " +
				"M 10,2.5 A 1.3,1.3 0 1 1 10.01,2.5 Z  " +
				"M 13,4 A 1.3,1.3 0 1 1 13.01,4 Z  " +
				"M 8.5,7.5 C 6.5,7.5 5,9 5,11 C 5,13 6.5,14.5 8.5,14.5 C 10.5,14.5 12,13 12,11 C 12,9 10.5,7.5 8.5,7.5 Z"),
			["tree"] = G("M 8,1 L 3.5,7 H 5.5 L 2,12 H 14 L 10.5,7 H 12.5 Z  M 7,12 V 15 H 9 V 12"),
			["chill"] = G(
				"M 8,1 V 15  M 2.5,4.5 L 13.5,11.5  M 13.5,4.5 L 2.5,11.5  " +
				"M 8,1 L 6.2,2.8  M 8,1 L 9.8,2.8  M 8,15 L 6.2,13.2  M 8,15 L 9.8,13.2  " +
				"M 2.5,4.5 L 4.5,4.8  M 2.5,4.5 L 3,6.3  " +
				"M 13.5,4.5 L 11.5,4.8  M 13.5,4.5 L 13,6.3  " +
				"M 2.5,11.5 L 4.5,11.2  M 2.5,11.5 L 3,9.7  " +
				"M 13.5,11.5 L 11.5,11.2  M 13.5,11.5 L 13,9.7"),
		};

		foreach (var g in dict.Values)
			g.Freeze();

		IconMap = dict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
	}

	private static Geometry G(string data) => Geometry.Parse(data);
}
