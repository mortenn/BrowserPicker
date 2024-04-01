using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace BrowserPicker.Converter;

public sealed class IconFileToImageConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value?.ToString() is not { } iconPath)
			return GetDefaultIcon();
			
		if (cache.TryGetValue(iconPath, out var cachedIcon))
		{
			return cachedIcon;
		}

		if (string.IsNullOrWhiteSpace(iconPath))
		{
			return GetDefaultIcon();
		}

		var realIconPath = iconPath.Trim('"', '\'', ' ', '\t', '\r', '\n');
		try
		{
			if (!File.Exists(realIconPath) && realIconPath.Contains('%'))
				realIconPath = Environment.ExpandEnvironmentVariables(realIconPath);

			if (!File.Exists(realIconPath))
				return GetDefaultIcon();

			Stream icon;
			if (realIconPath.EndsWith(".exe") || realIconPath.EndsWith(".dll"))
			{
				var iconData = Icon.ExtractAssociatedIcon(realIconPath)?.ToBitmap();
				if (iconData == null)
					return GetDefaultIcon();
				icon = new MemoryStream();
				iconData.Save(icon, ImageFormat.Png);
			}
			else
			{
				icon = File.Open(realIconPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			}

			cache.Add(iconPath, BitmapFrame.Create(icon));
			return cache[iconPath];
		}
		catch
		{
			// ignored
		}
		return GetDefaultIcon();
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return null;
	}

	private static object GetDefaultIcon()
	{
		return Application.Current.TryFindResource("DefaultIcon");
	}

	private readonly Dictionary<string, BitmapFrame> cache = [];
}
