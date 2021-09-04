using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BrowserPicker.Lib
{
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
				if (key.GetValue(name) != null)
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

		private readonly static Dictionary<Type, RegistryValueKind> TypeMap = new Dictionary<Type, RegistryValueKind>
		{
			{ typeof(string), RegistryValueKind.String },
			{ typeof(int), RegistryValueKind.DWord },
			{ typeof(long), RegistryValueKind.QWord },
		};
	}
}
