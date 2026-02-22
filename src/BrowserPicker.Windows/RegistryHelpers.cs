using Microsoft.Win32;
using System.Runtime.CompilerServices;

namespace BrowserPicker.Windows;

public static class RegistryHelpers
{
	/// <summary>
	/// Retrieves the value associated with the specified name from the RegistryKey.
	/// </summary>
	/// <typeparam name="T">The type of the value that is expected to be retrieved.</typeparam>
	/// <param name="key">The RegistryKey to retrieve the value from.</param>
	/// <param name="defaultValue">The default value to return if the requested value doesn't exist or an error occurs.</param>
	/// <param name="name">The name of the value to retrieve. Defaults to the name of the calling member.</param>
	/// <returns>The retrieved value cast to type <typeparamref name="T"/>, or the <paramref name="defaultValue"/> if retrieval fails.</returns>
	public static T? Get<T>(this RegistryKey key, T? defaultValue = default, [CallerMemberName] string? name = null)
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

	/// <summary>
	/// Retrieves a boolean value associated with the specified name from the RegistryKey.
	/// </summary>
	/// <param name="key">The RegistryKey to retrieve the value from.</param>
	/// <param name="defaultValue">The default value to return if the requested value doesn't exist or an error occurs.</param>
	/// <param name="name">The name of the value to retrieve. Defaults to the name of the calling member.</param>
	/// <returns>The retrieved boolean value, or <paramref name="defaultValue"/> if retrieval fails.</returns>
	public static bool GetBool(this RegistryKey key, bool defaultValue = false, [CallerMemberName] string? name = null)
	{
		var value = key.GetValue(name);
		if (value == null)
			return defaultValue;
		
		return (int)value == 1;
	}

	/// <summary>
	/// Retrieves browser-related information (name, icon path, and shell command) from the specified RegistryKey.
	/// </summary>
	/// <param name="key">The RegistryKey containing the browser information.</param>
	/// <returns>
	/// A tuple containing the browser name, icon path, and shell command, or null for each if retrieval fails.
	/// </returns>
	public static (string? name, string? icon, string? shell) GetBrowser(this RegistryKey key)
	{
		try
		{
			var name = (string?)key.GetValue(null);

			var icon = (string?)key.OpenSubKey("DefaultIcon", false)?.GetValue(null);
			if (icon?.Contains(',') ?? false)
				icon = icon.Split(',')[0];
			var shell = (string?)key.OpenSubKey(@"shell\open\command", false)?.GetValue(null);

			return (name, icon, shell);
		}
		catch
		{
			return (null, null, null);
		}
	}
}