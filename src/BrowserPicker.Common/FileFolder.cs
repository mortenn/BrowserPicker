using System;
using System.IO;

namespace BrowserPicker.Common;

/// <summary>
/// Matches local and network files by the folder they are in, for <see cref="MatchType.Folder"/> rules.
/// </summary>
public static class FileFolder
{
	/// <summary>
	/// The folder of a file URL, ending with a backslash (for example <c>C:\Reports\</c>), or null for other URLs.
	/// </summary>
	/// <param name="url">The URL being opened.</param>
	public static string? Of(Uri? url)
	{
		if (url is not { IsFile: true })
		{
			return null;
		}

		var folder = Path.GetDirectoryName(url.LocalPath);
		return string.IsNullOrEmpty(folder) ? null : WithTrailingSeparator(folder);
	}

	/// <summary>
	/// Returns the length of <paramref name="folder"/> when <paramref name="url"/> is a file inside it or one of
	/// its subfolders, otherwise 0. The comparison ignores case, like Windows paths, and only matches whole folder
	/// names, so <c>C:\Temp</c> does not match <c>C:\Temp2\x.html</c>. The folder may be written as a path or as a
	/// <c>file:///</c> URL, and matches whether the file was passed as a path or as a URL.
	/// </summary>
	/// <param name="url">The URL being opened.</param>
	/// <param name="folder">The folder from the rule.</param>
	public static int MatchLength(Uri url, string folder)
	{
		if (!url.IsFile)
		{
			return 0;
		}

		var normalized = Normalize(folder);
		return normalized != null && url.LocalPath.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)
			? normalized.Length
			: 0;
	}

	/// <summary>
	/// Turns a rule pattern into an absolute folder path ending with a backslash, or null when it is not one.
	/// Relative paths are rejected rather than resolved against the current directory.
	/// </summary>
	private static string? Normalize(string folder)
	{
		var path = folder.Trim();
		if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
		{
			if (!Uri.TryCreate(path, UriKind.Absolute, out var uri) || !uri.IsFile)
			{
				return null;
			}
			path = uri.LocalPath;
		}

		path = path.Replace('/', '\\');
		var isDrivePath = path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\';
		var isNetworkPath = path.StartsWith(@"\\", StringComparison.Ordinal) && path.Length > 2;
		return isDrivePath || isNetworkPath ? WithTrailingSeparator(path) : null;
	}

	private static string WithTrailingSeparator(string path) => path.EndsWith('\\') ? path : path + '\\';
}
