using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BrowserPicker.UI.Converters;

/// <summary>
/// Converts an icon file path or byte array to a WPF image for display (e.g. browser icons).
/// </summary>
public sealed class IconFileToImageConverter : IValueConverter
{
	/// <inheritdoc />
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is byte[] bytes && bytes.Length > 0)
		{
			try
			{
				using var stream = new MemoryStream(bytes);
				var frame = BitmapFrame.Create(
					stream,
					BitmapCreateOptions.PreservePixelFormat,
					BitmapCacheOption.OnLoad
				);
				frame.Freeze();
				return frame;
			}
			catch
			{
				return GetDefaultIcon();
			}
		}

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

			ImageSource image;
			if (realIconPath.EndsWith(".exe") || realIconPath.EndsWith(".dll"))
			{
				// Read the icon handle straight into a WPF bitmap; no GDI+ bitmap or PNG round trip.
				using var associated = Icon.ExtractAssociatedIcon(realIconPath);
				if (associated == null)
					return GetDefaultIcon();
				image = Imaging.CreateBitmapSourceFromHIcon(
					associated.Handle,
					Int32Rect.Empty,
					BitmapSizeOptions.FromEmptyOptions()
				);
				image.Freeze();
			}
			else
			{
				image = BitmapFrame.Create(File.Open(realIconPath, FileMode.Open, FileAccess.Read, FileShare.Read));
			}

			cache.Add(iconPath, image);
			return image;
		}
		catch
		{
			// ignored
		}
		return GetDefaultIcon();
	}

	/// <inheritdoc />
	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return null;
	}

	private static object GetDefaultIcon()
	{
		return Application.Current.TryFindResource("DefaultIcon");
	}

	// Shared by every converter instance (picker and configuration views); only used on the UI thread.
	private static readonly Dictionary<string, ImageSource> cache = [];
}
