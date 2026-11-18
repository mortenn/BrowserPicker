using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BrowserPicker.UI;

/// <summary>
/// Answers "is this browser running?" from one shared, short-lived process snapshot.
/// </summary>
/// <remarks>
/// Each <see cref="Process.GetProcessesByName(string)"/> call enumerates every process on the machine.
/// The picker asks once per browser while it renders, which added up to a noticeable delay before the
/// window appeared. The snapshot is reused for <see cref="MaxAge"/>, which is far shorter than any user-visible change.
/// </remarks>
internal static class RunningProcesses
{
	private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(2);
	private static readonly object Gate = new();
	private static readonly int CurrentSession = Process.GetCurrentProcess().SessionId;
	private static Process[] snapshot = [];
	private static long taken_at;

	/// <summary>
	/// True when a process named <paramref name="processName"/> runs in the current session.
	/// </summary>
	public static bool AnyInSession(string processName) =>
		Matching(processName).Any(p => Check(() => p.SessionId == CurrentSession));

	/// <summary>
	/// True when <paramref name="executablePath"/> runs in the current session with a main window.
	/// </summary>
	public static bool HasMainWindow(string executablePath) =>
		Matching(Path.GetFileNameWithoutExtension(executablePath))
			.Any(p =>
				Check(() =>
					p.SessionId == CurrentSession && p.MainWindowHandle != 0 && p.MainModule?.FileName == executablePath
				)
			);

	private static Process[] Matching(string processName)
	{
		lock (Gate)
		{
			if (Stopwatch.GetElapsedTime(taken_at) > MaxAge)
			{
				foreach (var process in snapshot)
				{
					process.Dispose();
				}
				snapshot = Process.GetProcesses();
				taken_at = Stopwatch.GetTimestamp();
			}
			return snapshot
				.Where(p => string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
				.ToArray();
		}
	}

	private static bool Check(Func<bool> condition)
	{
		try
		{
			return condition();
		}
		catch
		{
			// The process exited or denied access after the snapshot was taken.
			return false;
		}
	}
}
