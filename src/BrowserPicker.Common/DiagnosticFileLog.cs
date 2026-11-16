using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace BrowserPicker.Common;

/// <summary>
/// Opt-in diagnostic log written to daily files, shared by every BrowserPicker process.
/// </summary>
/// <remarks>
/// Each link click starts a new short-lived BrowserPicker process, so an in-memory log cannot show what
/// happened on earlier clicks. This log is off by default, redacts URLs (see <see cref="DiagnosticLogRedactor"/>),
/// deletes files older than the retention period on every start, and caps each file's size.
/// Entries logged before <see cref="Configure"/> is called are held in memory and written only if logging
/// turns out to be enabled.
/// </remarks>
public sealed class DiagnosticFileLog
{
	/// <summary>Largest size a single daily file may grow to; later entries that day are dropped.</summary>
	public const long MaxFileBytes = 5 * 1024 * 1024;

	/// <summary>Shortest retention period, in days.</summary>
	public const int MinRetentionDays = 1;

	/// <summary>Longest retention period, in days.</summary>
	public const int MaxRetentionDays = 30;

	private const string FilePrefix = "browserpicker-";
	private const string FileSearchPattern = FilePrefix + "*.log";
	private const int MaxPendingEntries = 500;

	private readonly Lock gate = new();
	private readonly List<PendingEntry> pending = [];
	private readonly Func<DateTimeOffset> clock;
	private readonly string? user_profile_path;
	private readonly int process_id = Environment.ProcessId;
	private bool configured;
	private bool enabled;
	private bool include_full_urls;
	private int retention_days = MinRetentionDays;
	private bool size_limit_noted;

	/// <summary>
	/// Creates a log writing to <see cref="DefaultDirectory"/>.
	/// </summary>
	public DiagnosticFileLog()
		: this(DefaultDirectory, () => DateTimeOffset.Now, null) { }

	/// <summary>
	/// Creates a log writing to <paramref name="directory"/>.
	/// </summary>
	/// <param name="directory">Folder that holds the log files.</param>
	/// <param name="clock">Source of the current time.</param>
	/// <param name="userProfilePath">Profile folder to redact; null uses the current user's.</param>
	public DiagnosticFileLog(string directory, Func<DateTimeOffset> clock, string? userProfilePath)
	{
		Directory = directory;
		this.clock = clock;
		user_profile_path = userProfilePath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
	}

	/// <summary>
	/// Default folder: <c>%LOCALAPPDATA%\BrowserPicker\logs</c>.
	/// </summary>
	public static string DefaultDirectory =>
		Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"BrowserPicker",
			"logs"
		);

	/// <summary>Folder that holds the log files.</summary>
	public string Directory { get; }

	/// <summary>
	/// True while entries are accepted: before configuration (they are held in memory) or when enabled.
	/// </summary>
	public bool IsAccepting
	{
		get
		{
			lock (gate)
			{
				return !configured || enabled;
			}
		}
	}

	/// <summary>
	/// Applies the user's settings, deletes expired files and writes or discards entries held in memory.
	/// </summary>
	/// <param name="enable">Whether entries are written to disk.</param>
	/// <param name="includeFullUrls">Whether URLs are written without redaction.</param>
	/// <param name="retentionDays">Days to keep files; clamped to the allowed range.</param>
	public void Configure(bool enable, bool includeFullUrls, int retentionDays)
	{
		PendingEntry[] flush;
		lock (gate)
		{
			configured = true;
			enabled = enable;
			include_full_urls = includeFullUrls;
			retention_days = ClampRetentionDays(retentionDays);
			flush = enable ? pending.ToArray() : Array.Empty<PendingEntry>();
			pending.Clear();
		}

		DeleteExpiredFiles();
		foreach (var entry in flush)
		{
			WriteEntry(entry);
		}
	}

	/// <summary>
	/// Clamps a retention period to <see cref="MinRetentionDays"/>..<see cref="MaxRetentionDays"/>.
	/// </summary>
	public static int ClampRetentionDays(int days) => Math.Clamp(days, MinRetentionDays, MaxRetentionDays);

	/// <summary>
	/// Records a log entry.
	/// </summary>
	public void Write(LogLevel level, string category, EventId eventId, string message, Exception? exception)
	{
		var entry = new PendingEntry(clock(), level, category, eventId, message, exception?.ToString());
		lock (gate)
		{
			if (!configured)
			{
				if (pending.Count < MaxPendingEntries)
				{
					pending.Add(entry);
				}
				return;
			}
			if (!enabled)
			{
				return;
			}
		}
		WriteEntry(entry);
	}

	/// <summary>
	/// Lists the log files, newest first.
	/// </summary>
	public IReadOnlyList<FileInfo> GetLogFiles()
	{
		try
		{
			var directory = new DirectoryInfo(Directory);
			if (!directory.Exists)
			{
				return [];
			}
			return [.. directory.EnumerateFiles(FileSearchPattern).OrderByDescending(f => f.LastWriteTimeUtc)];
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return [];
		}
	}

	/// <summary>
	/// Deletes every log file.
	/// </summary>
	/// <returns>The number of files that could not be deleted (for example because another process holds them).</returns>
	public int DeleteAllFiles()
	{
		lock (gate)
		{
			size_limit_noted = false;
			return GetLogFiles().Count(file => !TryDelete(file));
		}
	}

	/// <summary>
	/// Deletes files last written longer ago than the retention period.
	/// </summary>
	public void DeleteExpiredFiles()
	{
		int days;
		lock (gate)
		{
			days = retention_days;
		}
		var cutoff = clock().AddDays(-days).UtcDateTime;
		foreach (var file in GetLogFiles().Where(f => f.LastWriteTimeUtc < cutoff))
		{
			_ = TryDelete(file);
		}
	}

	private void WriteEntry(PendingEntry entry)
	{
		bool fullUrls;
		lock (gate)
		{
			fullUrls = include_full_urls;
		}

		var builder = new StringBuilder();
		builder
			.Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
			.Append(" [")
			.Append(LevelCode(entry.Level))
			.Append("] pid=")
			.Append(process_id)
			.Append(' ')
			.Append(entry.Category.Split('.').LastOrDefault() ?? entry.Category);
		if (entry.EventId.Id != 0)
		{
			builder.Append(" #").Append(entry.EventId.Id);
		}
		builder.Append(": ").Append(Sanitize(entry.Message.ReplaceLineEndings(" "), fullUrls));
		if (entry.Exception != null)
		{
			foreach (var line in Sanitize(entry.Exception, fullUrls).Split('\n'))
			{
				builder.AppendLine().Append("    ").Append(line.TrimEnd('\r'));
			}
		}
		builder.AppendLine();

		AppendToFile(builder.ToString(), entry.Timestamp);
	}

	private string Sanitize(string text, bool fullUrls) =>
		DiagnosticLogRedactor.Redact(text, fullUrls, user_profile_path);

	private void AppendToFile(string text, DateTimeOffset timestamp)
	{
		var path = Path.Combine(
			Directory,
			FilePrefix + timestamp.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log"
		);
		lock (gate)
		{
			try
			{
				System.IO.Directory.CreateDirectory(Directory);
				var file = new FileInfo(path);
				if (file.Exists && file.Length >= MaxFileBytes)
				{
					if (size_limit_noted)
					{
						return;
					}
					size_limit_noted = true;
					text =
						$"{timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [WRN] pid={process_id} DiagnosticFileLog: size limit reached, further entries from this process are dropped{Environment.NewLine}";
				}

				AppendWithRetry(path, Encoding.UTF8.GetBytes(text));
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// Diagnostics must never break the application.
				Debug.WriteLine($"Diagnostic log write failed: {e.Message}");
			}
		}
	}

	private static void AppendWithRetry(string path, byte[] bytes)
	{
		// Several BrowserPicker processes may write to the same file at once.
		for (var attempt = 1; ; attempt++)
		{
			try
			{
				using var stream = new FileStream(
					path,
					FileMode.Append,
					FileAccess.Write,
					FileShare.ReadWrite | FileShare.Delete
				);
				stream.Write(bytes);
				return;
			}
			catch (IOException) when (attempt < 5)
			{
				Thread.Sleep(10 * attempt);
			}
		}
	}

	private static bool TryDelete(FileInfo file)
	{
		try
		{
			file.Delete();
			return true;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return false;
		}
	}

	private static string LevelCode(LogLevel level) =>
		level switch
		{
			LogLevel.Trace => "TRC",
			LogLevel.Debug => "DBG",
			LogLevel.Information => "INF",
			LogLevel.Warning => "WRN",
			LogLevel.Error => "ERR",
			LogLevel.Critical => "CRT",
			_ => "LOG",
		};

	private sealed record PendingEntry(
		DateTimeOffset Timestamp,
		LogLevel Level,
		string Category,
		EventId EventId,
		string Message,
		string? Exception
	);
}

/// <summary>
/// Logger provider that forwards Debug-and-above entries to a <see cref="DiagnosticFileLog"/>.
/// </summary>
public sealed class DiagnosticFileLoggerProvider(DiagnosticFileLog log) : ILoggerProvider
{
	/// <inheritdoc />
	public ILogger CreateLogger(string categoryName) => new DiagnosticFileLogger(categoryName, log);

	/// <inheritdoc />
	public void Dispose() { }

	private sealed class DiagnosticFileLogger(string categoryName, DiagnosticFileLog log) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) =>
			logLevel is >= LogLevel.Debug and < LogLevel.None && log.IsAccepting;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter
		)
		{
			if (!IsEnabled(logLevel))
			{
				return;
			}
			var message = formatter(state, exception);
			if (string.IsNullOrWhiteSpace(message) && exception == null)
			{
				return;
			}
			log.Write(logLevel, categoryName, eventId, message, exception);
		}
	}
}
