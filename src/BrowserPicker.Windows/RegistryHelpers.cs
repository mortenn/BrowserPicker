using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
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
	/// Sets a value in the RegistryKey with the specified name and type.
	/// </summary>
	/// <typeparam name="T">The type of the value to store.</typeparam>
	/// <param name="key">The RegistryKey to store the value in.</param>
	/// <param name="value">The value to set in the registry. If null, the value will be deleted.</param>
	/// <param name="name">The name of the value to set. Defaults to the name of the calling member.</param>
	public static void Set<T>(this RegistryKey key, T value, [CallerMemberName] string? name = null)
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

	/// <summary>
	/// Opens a subkey of the specified RegistryKey at the specified path.
	/// </summary>
	/// <param name="key">The RegistryKey to open the subkey from.</param>
	/// <param name="path">The path to the subkey to open.</param>
	/// <returns>The opened subkey, or null if it doesn't exist.</returns>
	public static RegistryKey? SubKey(this RegistryKey key, params string[] path)
	{
		return key.OpenSubKey(Path.Combine(path), true);
	}

	/// <summary>
	/// Ensures that a subkey exists at the specified path, creating it if necessary.
	/// </summary>
	/// <param name="key">The RegistryKey to ensure the subkey for.</param>
	/// <param name="path">The path to the subkey to create or open.</param>
	/// <returns>The created or opened subkey.</returns>
	public static RegistryKey EnsureSubKey(this RegistryKey key, params string[] path)
	{
		return key.CreateSubKey(Path.Combine(path), RegistryKeyPermissionCheck.ReadWriteSubTree);
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

	/// <summary>
	/// Opens or creates a subkey at the specified path under the specified RegistryKey.
	/// </summary>
	/// <param name="key">The RegistryKey to create the subkey in.</param>
	/// <param name="path">The path to the subkey to open or create.</param>
	/// <returns>The created or opened subkey.</returns>
	public static RegistryKey Open(this RegistryKey key, params string[] path)
	{
		return key.CreateSubKey(Path.Combine(path), true);
	}

	/// <summary>
	/// A mapping of .NET value types to their corresponding RegistryValueKind.
	/// </summary>
	private static readonly Dictionary<Type, RegistryValueKind> TypeMap = new()
	{
		{ typeof(string), RegistryValueKind.String },
		{ typeof(int), RegistryValueKind.DWord },
		{ typeof(long), RegistryValueKind.QWord },
		{ typeof(string[]), RegistryValueKind.MultiString }
	};
}