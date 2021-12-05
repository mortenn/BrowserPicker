using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace BrowserPicker.Converter
{
	public class IconFileToImageConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				return null;
			
			var iconPath = value.ToString();
			if (cache.ContainsKey(iconPath))
			{
				return cache[iconPath];
			}

			if (string.IsNullOrWhiteSpace(iconPath))
			{
				return null;
			}

			var realIconPath = iconPath.Trim('"', '\'', ' ', '\t', '\r', '\n');
			try
			{
				if (!File.Exists(realIconPath) && realIconPath.Contains('%'))
					realIconPath = Environment.ExpandEnvironmentVariables(realIconPath);

				if (!File.Exists(realIconPath))
					return null;

				Stream icon;
				if (realIconPath.EndsWith(".exe") || realIconPath.EndsWith(".dll"))
				{
					var iconData = Icon.ExtractAssociatedIcon(realIconPath)?.ToBitmap();
					if (iconData == null)
						return null;
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
			return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}

		private readonly Dictionary<string, BitmapFrame> cache = new();
	}
}
