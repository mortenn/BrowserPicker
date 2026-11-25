using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace BrowserPicker.Windows;

/// <summary>
/// Keeps the user's abuse.ch Auth-Key in Windows Credential Manager, for the current user on this computer.
/// It is never written to settings.json, so exports, backups, copied settings and feedback reports don't contain it.
/// </summary>
public static class UrlhausAuthKey
{
	public const string TargetName = "BrowserPicker:URLhaus Auth-Key";

	private const uint CredTypeGeneric = 1;
	private const uint CredPersistLocalMachine = 2;
	private const int ErrorNotFound = 1168;

	/// <summary>
	/// The saved key, or null when none is saved or it can't be read.
	/// </summary>
	public static string? Read()
	{
		if (!CredRead(TargetName, CredTypeGeneric, 0, out var pointer))
		{
			return null;
		}

		try
		{
			var credential = Marshal.PtrToStructure<Credential>(pointer);
			if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
			{
				return null;
			}

			var key = Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
			return string.IsNullOrWhiteSpace(key) ? null : key;
		}
		finally
		{
			CredFree(pointer);
		}
	}

	public static bool Exists => Read() != null;

	/// <summary>
	/// Saves or replaces the key. Returns false when Windows refuses to store it.
	/// </summary>
	public static bool Save(string key)
	{
		key = key.Trim();
		if (key.Length == 0)
		{
			return false;
		}

		var bytes = Encoding.Unicode.GetBytes(key);
		var blob = Marshal.AllocHGlobal(bytes.Length);
		try
		{
			Marshal.Copy(bytes, 0, blob, bytes.Length);
			var credential = new Credential
			{
				Type = CredTypeGeneric,
				TargetName = TargetName,
				CredentialBlobSize = (uint)bytes.Length,
				CredentialBlob = blob,
				Persist = CredPersistLocalMachine,
				UserName = "abuse.ch",
			};
			return CredWrite(ref credential, 0);
		}
		finally
		{
			Marshal.Copy(new byte[bytes.Length], 0, blob, bytes.Length);
			Marshal.FreeHGlobal(blob);
			Array.Clear(bytes);
		}
	}

	/// <summary>
	/// Removes the key. Returns true when no key is left.
	/// </summary>
	public static bool Delete() =>
		CredDelete(TargetName, CredTypeGeneric, 0) || Marshal.GetLastWin32Error() == ErrorNotFound;

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct Credential
	{
		public uint Flags;
		public uint Type;
		public string TargetName;
		public string? Comment;
		public FILETIME LastWritten;
		public uint CredentialBlobSize;
		public IntPtr CredentialBlob;
		public uint Persist;
		public uint AttributeCount;
		public IntPtr Attributes;
		public string? TargetAlias;
		public string? UserName;
	}

	[DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

	[DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool CredWrite(ref Credential credential, uint flags);

	[DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool CredDelete(string target, uint type, uint flags);

	[DllImport("advapi32.dll", SetLastError = true)]
	private static extern void CredFree(IntPtr buffer);
}
