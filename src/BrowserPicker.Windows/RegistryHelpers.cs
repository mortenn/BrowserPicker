using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace BrowserPicker.Windows;

public static class RegistryHelpers
{
	public static T Get<T>(this RegistryKey key, T defaultValue = default, [CallerMemberName] string name = null)
	{
		try
		{
			if (typeof(T) == typeof(bool))
				return (T)(object)(((int?)key.GetValue(name) ?? 0) == 1);

			var value = key.GetValue(name);
			return value == null ? defaultValue : (T)value;
		}
		catch
		{
			return defaultValue;
		}
	}

	public static void Set<T>(this RegistryKey key, T value, [CallerMemberName] string name = null)
	{
		if (value == null)
		{
			if (name != null && key.GetValue(name) != null)
			{
				key.DeleteValue(name);
			}
			return;
		}
		if (typeof(T) == typeof(bool))
		{
			key.SetValue(name, (bool)(object)value ? 1 : 0, RegistryValueKind.DWord);
			return;
		}
		if (!TypeMap.ContainsKey(typeof(T)))
		{
			return;
		}
		key.SetValue(name, value, TypeMap[typeof(T)]);
	}

	public static RegistryKey SubKey(this RegistryKey key, params string[] path)
	{
		return key.OpenSubKey(Path.Combine(path), true);
	}

	public static (string name, string icon, string shell) GetBrowser(this RegistryKey key)
	{
		try
		{
			var name = (string)key.GetValue(null);

			var icon = (string)key.OpenSubKey("DefaultIcon", false)?.GetValue(null);
			if (icon?.Contains(',') ?? false)
				icon = icon.Split(',')[0];
			var shell = (string)key.OpenSubKey(@"shell\open\command", false)?.GetValue(null);

			return (name, icon, shell);
		}
		catch
		{
			return (null, null, null);
		}
	}

	public static RegistryKey Open(this RegistryKey key, params string[] path)
	{
		return key.CreateSubKey(Path.Combine(path), true);
	}

	private static readonly Dictionary<Type, RegistryValueKind> TypeMap = new()
	{
		{ typeof(string), RegistryValueKind.String },
		{ typeof(int), RegistryValueKind.DWord },
		{ typeof(long), RegistryValueKind.QWord }
	};
}