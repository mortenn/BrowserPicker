using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace BrowserPicker.Common;

/// <summary>
/// Structured message segment used to highlight inserted values separately from template text.
/// </summary>
public sealed record InMemoryLogSegment(string Text, bool IsValue);

/// <summary>
/// Structured runtime log entry for the in-memory feedback viewer.
/// </summary>
public sealed record InMemoryLogEntry(DateTimeOffset Timestamp, LogLevel Level, string Category, EventId EventId, string Message, IReadOnlyList<InMemoryLogSegment> Segments)
{
	public string TimestampDisplay => Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");

	public string LevelDisplay => Level switch
	{
		LogLevel.Trace => "TRC",
		LogLevel.Debug => "DBG",
		LogLevel.Information => "INF",
		LogLevel.Warning => "WRN",
		LogLevel.Error => "ERR",
		LogLevel.Critical => "CRT",
		_ => "LOG"
	};

	public string CategoryDisplay => Category.Split('.').LastOrDefault() ?? Category;

	public string EventDisplay
	{
		get
		{
			var eventName = string.IsNullOrWhiteSpace(EventId.Name) ? string.Empty : $"/{EventId.Name}";
			return $"{EventId.Id}{eventName}";
		}
	}

	public string SearchText => $"{TimestampDisplay} {LevelDisplay} {Level} {Category} {CategoryDisplay} {EventDisplay} {Message}";
}

/// <summary>
/// Stores a bounded in-memory snapshot of runtime log messages.
/// </summary>
public sealed class InMemoryLogBuffer
{
	private readonly Lock gate = new();
	private readonly Queue<InMemoryLogEntry> entries = new();

	public InMemoryLogBuffer(int capacity = 500)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
		Capacity = capacity;
	}

	public int Capacity { get; }

	public event EventHandler? Updated;

	public void Append(DateTimeOffset timestamp, string category, LogLevel level, EventId eventId, string message,
		IReadOnlyList<InMemoryLogSegment>? segments, Exception? exception)
	{
		lock (gate)
		{
			Enqueue(new InMemoryLogEntry(timestamp, level, category, eventId, message,
				segments ?? [new InMemoryLogSegment(message, false)]));

			if (exception != null)
			{
				var exceptionText = exception.ToString();
				Enqueue(new InMemoryLogEntry(timestamp, level, category, eventId, exceptionText,
					[new InMemoryLogSegment(exceptionText, false)]));
			}
		}

		Updated?.Invoke(this, EventArgs.Empty);
	}

	public IReadOnlyList<InMemoryLogEntry> GetEntries()
	{
		lock (gate)
		{
			return [.. entries];
		}
	}

	private void Enqueue(InMemoryLogEntry entry)
	{
		entries.Enqueue(entry);
		while (entries.Count > Capacity)
		{
			entries.Dequeue();
		}
	}
}

/// <summary>
/// Logging provider that mirrors application logs into <see cref="InMemoryLogBuffer"/>.
/// </summary>
public sealed class InMemoryLoggerProvider(InMemoryLogBuffer buffer) : ILoggerProvider
{
	public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName, buffer);

	public void Dispose()
	{
	}

	private sealed class InMemoryLogger(string categoryName, InMemoryLogBuffer buffer) : ILogger
	{
		public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

		public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
			Func<TState, Exception?, string> formatter)
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

			buffer.Append(DateTimeOffset.UtcNow, categoryName, logLevel, eventId, message, CreateSegments(state, message), exception);
		}

		private static List<InMemoryLogSegment> CreateSegments<TState>(TState state, string fallbackMessage)
		{
			if (state is not IEnumerable<KeyValuePair<string, object?>> structuredState)
			{
				return [new InMemoryLogSegment(fallbackMessage, false)];
			}

			var values = structuredState.ToList();
			var originalFormat = values.FirstOrDefault(kv => kv.Key == "{OriginalFormat}").Value as string;
			if (string.IsNullOrWhiteSpace(originalFormat))
			{
				return [new InMemoryLogSegment(fallbackMessage, false)];
			}

			var arguments = values
				.Where(kv => kv.Key != "{OriginalFormat}")
				.Select(kv => FormatValue(kv.Value))
				.ToList();

			var segments = ParseTemplate(originalFormat, arguments);
			return segments.Count > 0 ? segments : [new InMemoryLogSegment(fallbackMessage, false)];
		}

		private static List<InMemoryLogSegment> ParseTemplate(string template, List<string> arguments)
		{
			var segments = new List<InMemoryLogSegment>();
			var literal = new System.Text.StringBuilder();
			var argumentIndex = 0;

			for (var i = 0; i < template.Length; i++)
			{
				var ch = template[i];
				switch (ch)
				{
					case '{':
						if (i + 1 < template.Length && template[i + 1] == '{')
						{
							literal.Append('{');
							i++;
							break;
						}

						var end = template.IndexOf('}', i + 1);
						if (end < 0)
						{
							literal.Append(ch);
							break;
						}

						if (literal.Length > 0)
						{
							segments.Add(new InMemoryLogSegment(literal.ToString(), false));
							literal.Clear();
						}

						var value = argumentIndex < arguments.Count ? arguments[argumentIndex] : template[(i + 1)..end];
						segments.Add(new InMemoryLogSegment(value, true));
						argumentIndex++;
						i = end;
						break;
					case '}' when i + 1 < template.Length && template[i + 1] == '}':
						literal.Append('}');
						i++;
						break;
					default:
						literal.Append(ch);
						break;
				}
			}

			if (literal.Length > 0)
			{
				segments.Add(new InMemoryLogSegment(literal.ToString(), false));
			}

			return segments;
		}

		private static string FormatValue(object? value) => value switch
		{
			null => "null",
			IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
			_ => value.ToString() ?? "null"
		};

		private sealed class NullScope : IDisposable
		{
			public static readonly NullScope Instance = new();

			public void Dispose()
			{
			}
		}
	}
}
