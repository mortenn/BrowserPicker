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
			if (!string.IsNullOrEmpty(iconPath))
			{
				var _iconPath = iconPath.Trim(new[] { '"', '\'', ' ', '\t', '\r', '\n' });
				try
				{
					if (!File.Exists(_iconPath) && _iconPath.Contains("%"))
						_iconPath = Environment.ExpandEnvironmentVariables(_iconPath);

					if (!File.Exists(_iconPath))
						return null;

					Stream icon = null;
					if (_iconPath.EndsWith(".exe") || _iconPath.EndsWith(".dll"))
					{
						var iconData = Icon.ExtractAssociatedIcon(_iconPath)?.ToBitmap();
						if (iconData == null)
							return null;
						icon = new MemoryStream();
						iconData.Save(icon, ImageFormat.Png);
					}
					else
					{
						icon = File.Open(_iconPath, FileMode.Open, FileAccess.Read, FileShare.Read);
					}
					if (icon != null)
					{
						cache.Add(iconPath, BitmapFrame.Create(icon));
						return cache[iconPath];
					}
				}
				catch { }
			}
			return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}

		private Dictionary<string, BitmapFrame> cache = new Dictionary<string, BitmapFrame>();
	}
}
