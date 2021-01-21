using Microsoft.Win32;

namespace BrowserPicker.Configuration
{
	public static class RegistryHelpers
	{
		public static T Get<T>(this RegistryKey key, string name, T defaultValue = default )
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


		public static void Set<T>(this RegistryKey key, string name, T value)
		{
			if (typeof(T) == typeof(bool))
				key.SetValue(name, (bool)(object)value ? 1 : 0, RegistryValueKind.DWord);

			else if (typeof(T) == typeof(string))
				key.SetValue(name, value, RegistryValueKind.String);

			else if (typeof(T) == typeof(int))
				key.SetValue(name, value, RegistryValueKind.DWord);
		}
	}
}
