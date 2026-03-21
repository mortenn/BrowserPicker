using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using BrowserPicker.Framework;
using BrowserPicker.Windows;
using Microsoft.Extensions.Logging;

#if DEBUG
using JetBrains.Annotations;
// ReSharper disable StringLiteralTypo
#endif

namespace BrowserPicker.ViewModel;

/// <summary>
/// Owns feedback reporting and the live in-memory application log shown in settings.
/// </summary>
public sealed class FeedbackViewModel : ModelBase
{
	private static readonly JsonSerializerOptions JsonWriteIndented = new() { WriteIndented = true };

	private readonly IBrowserPickerConfiguration settings;
	private readonly InMemoryLogBuffer? runtime_log_buffer;
	private readonly ObservableCollection<InMemoryLogEntry> application_log_entries = [];
	private string application_log_filter = string.Empty;
	private ApplicationLogLevelOption selected_application_log_level_option;
	private DelegateCommand? copy_feedback_markdown;
	private DelegateCommand? report_bug;
	private DelegateCommand? copy_application_log;
	private DelegateCommand? clear_application_log_filter;

#if DEBUG
	[UsedImplicitly]
	public FeedbackViewModel()
		: this(new DesignTimeSettings(), CreateDesignTimeLogBuffer())
	{
	}
#endif

	public FeedbackViewModel(IBrowserPickerConfiguration settings, InMemoryLogBuffer? runtimeLogBuffer = null)
	{
		this.settings = settings;
		runtime_log_buffer = runtimeLogBuffer;
		selected_application_log_level_option = ApplicationLogLevelOptions.All.First(option =>
			option.MinimumLevel == LogLevel.Information);
		FilteredApplicationLogEntries = CollectionViewSource.GetDefaultView(application_log_entries);
		FilteredApplicationLogEntries.Filter = FilterApplicationLogEntry;
		if (runtime_log_buffer != null)
		{
			runtime_log_buffer.Updated += RuntimeLogBuffer_Updated;
		}

		SyncApplicationLogEntries();
	}

	public ICommand CopyFeedbackMarkdown => copy_feedback_markdown ??= new DelegateCommand(CopyFeedbackMarkdownToClipboard);

	public ICommand ReportBug => report_bug ??= new DelegateCommand(OpenBugReport);

	public ICommand CopyApplicationLog => copy_application_log ??= new DelegateCommand(CopyApplicationLogToClipboard);

	public ICommand ClearApplicationLogFilter => clear_application_log_filter ??= new DelegateCommand(() => ApplicationLogFilter = string.Empty);

	public ICollectionView FilteredApplicationLogEntries { get; }

	public ApplicationLogLevelOption SelectedApplicationLogLevelOption
	{
		get => selected_application_log_level_option;
		set
		{
			if (!SetProperty(ref selected_application_log_level_option, value))
			{
				return;
			}

			FilteredApplicationLogEntries.Refresh();
			OnPropertyChanged(nameof(VisibleApplicationLogEntryCount));
			OnPropertyChanged(nameof(ApplicationLogStatus));
		}
	}

	public string ApplicationLogFilter
	{
		get => application_log_filter;
		set
		{
			if (!SetProperty(ref application_log_filter, value))
			{
				return;
			}

			FilteredApplicationLogEntries.Refresh();
			OnPropertyChanged(nameof(VisibleApplicationLogEntryCount));
			OnPropertyChanged(nameof(ApplicationLogStatus));
			OnPropertyChanged(nameof(HasApplicationLogFilter));
		}
	}

	public bool HasApplicationLogFilter => !string.IsNullOrWhiteSpace(ApplicationLogFilter);

	public int ApplicationLogEntryCount => application_log_entries.Count;

	public int VisibleApplicationLogEntryCount => application_log_entries.Count(FilterApplicationLogEntry);

	public string ApplicationLogStatus => runtime_log_buffer == null
		? "Runtime logging is unavailable."
		: string.IsNullOrWhiteSpace(ApplicationLogFilter)
			&& VisibleApplicationLogEntryCount == ApplicationLogEntryCount
			? $"Live capture · {ApplicationLogEntryCount} entr{(ApplicationLogEntryCount == 1 ? "y" : "ies")} retained · capacity {runtime_log_buffer.Capacity}"
			: $"Live capture · showing {VisibleApplicationLogEntryCount} of {ApplicationLogEntryCount} entr{(ApplicationLogEntryCount == 1 ? "y" : "ies")} · {SelectedApplicationLogLevelOption.Label} · capacity {runtime_log_buffer.Capacity}";

	private void CopyFeedbackMarkdownToClipboard()
	{
		TrySetClipboardText(BuildFeedbackMarkdown());
	}

	private void CopyApplicationLogToClipboard()
	{
		TrySetClipboardText(BuildApplicationLogPlainText(markdownCompatible: true));
	}

	private void OpenBugReport()
	{
		try
		{
			var title = Uri.EscapeDataString("[Bug]: ");
			var body = Uri.EscapeDataString(BuildFeedbackMarkdown(maxLogCharacters: 2500, maxSettingsCharacters: 2500));
			var url = $"https://github.com/mortenn/BrowserPicker/issues/new?title={title}&body={body}";
			_ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
		}
		catch
		{
			// ignored
		}
	}

	private void RuntimeLogBuffer_Updated(object? sender, EventArgs e)
	{
		Application.Current?.Dispatcher.BeginInvoke(() =>
		{
			SyncApplicationLogEntries();
			OnPropertyChanged(nameof(ApplicationLogEntryCount));
			OnPropertyChanged(nameof(VisibleApplicationLogEntryCount));
			OnPropertyChanged(nameof(ApplicationLogStatus));
		});
	}

	private bool FilterApplicationLogEntry(object? item) =>
		item is InMemoryLogEntry entry
		&& entry.Level >= SelectedApplicationLogLevelOption.MinimumLevel
		&& (string.IsNullOrWhiteSpace(application_log_filter)
			|| entry.SearchText.Contains(application_log_filter, StringComparison.OrdinalIgnoreCase));

	private void SyncApplicationLogEntries()
	{
		application_log_entries.Clear();
		foreach (var entry in runtime_log_buffer?.GetEntries() ?? [])
		{
			application_log_entries.Add(entry);
		}

		FilteredApplicationLogEntries.Refresh();
	}

	private string BuildFeedbackMarkdown(int maxLogCharacters = int.MaxValue, int maxSettingsCharacters = int.MaxValue)
	{
		var version = GetApplicationVersion();
		var settingsDump = BuildFeedbackSettingsDumpJson(maxSettingsCharacters);

		var builder = new StringBuilder();
		builder.AppendLine("## Summary");
		builder.AppendLine("<!-- Describe the bug and what you expected to happen. -->");
		builder.AppendLine();
		builder.AppendLine("## Reproduction");
		builder.AppendLine("1. ");
		builder.AppendLine("2. ");
		builder.AppendLine("3. ");
		builder.AppendLine();
		builder.AppendLine("## Environment");
		builder.AppendLine($"- BrowserPicker version: `{version}`");
		builder.AppendLine($"- OS: `{Environment.OSVersion}`");
		builder.AppendLine($"- .NET runtime: `{Environment.Version}`");
		builder.AppendLine($"- 64-bit process: `{Environment.Is64BitProcess}`");
		builder.AppendLine($"- Browser count: `{settings.BrowserList.Count(b => !b.Removed)}`");
		builder.AppendLine($"- Disabled browsers: `{settings.BrowserList.Count(b => b is { Removed: false, Disabled: true })}`");
		builder.AppendLine($"- Default rules: `{settings.Defaults.Count(d => !d.Deleted)}`");
		builder.AppendLine();
		builder.AppendLine("## Settings Dump");
		builder.AppendLine("```json");
		builder.AppendLine(settingsDump);
		builder.AppendLine("```");
		builder.AppendLine();
		builder.AppendLine("## Application Log");
		builder.Append(BuildApplicationLogMarkdown(maxLogCharacters));

		return builder.ToString();
	}

	private string BuildFeedbackSettingsDumpJson(int maxCharacters)
	{
		var snapshot = new SerializableSettings(settings);
		if (settings is JsonAppSettings json)
		{
			snapshot.AutoCloseOnFocusLost = json.AutoCloseOnFocusLost;
		}

		var node = JsonSerializer.SerializeToNode(snapshot, JsonWriteIndented)?.AsObject();
		PruneJsonNode(node);
		if (node == null)
		{
			return "{}";
		}

		var settingsDump = node.ToJsonString(JsonWriteIndented);
		if (maxCharacters <= 0 || maxCharacters == int.MaxValue || settingsDump.Length <= maxCharacters)
		{
			return settingsDump;
		}

		return CompactJsonForLength(node, maxCharacters, JsonWriteIndented).ToJsonString(JsonWriteIndented);
	}

	private string BuildApplicationLogMarkdown(int maxCharacters)
	{
		if (application_log_entries.Count == 0)
		{
			return "_No runtime log entries yet._" + Environment.NewLine;
		}

		var header = new StringBuilder()
			.AppendLine("| Time | Level | Context | Event | Message |")
			.AppendLine("| --- | --- | --- | --- | --- |");

		if (maxCharacters is <= 0 or int.MaxValue)
		{
			var table = new StringBuilder(header.ToString());
			foreach (var entry in application_log_entries)
			{
				table.AppendLine(FormatApplicationLogRow(entry));
			}

			return table.ToString();
		}

		var rows = new List<string>();
		var currentLength = header.Length;
		for (var i = application_log_entries.Count - 1; i >= 0; i--)
		{
			var row = FormatApplicationLogRow(application_log_entries[i]) + Environment.NewLine;
			if (rows.Count > 0 && currentLength + row.Length > maxCharacters)
			{
				break;
			}

			rows.Add(row);
			currentLength += row.Length;
		}

		rows.Reverse();
		var truncated = rows.Count < application_log_entries.Count;
		var builder = new StringBuilder(header.ToString());
		if (truncated)
		{
			builder.AppendLine("| ... | ... | ... | ... | _Older log rows omitted for GitHub prefill._ |");
		}

		foreach (var row in rows)
		{
			builder.Append(row);
		}

		return builder.ToString();
	}

	private string BuildApplicationLogPlainText(bool markdownCompatible)
	{
		if (application_log_entries.Count == 0)
		{
			return "No runtime log entries yet.";
		}

		var builder = new StringBuilder();
		foreach (var entry in application_log_entries)
		{
			builder.AppendLine($"{entry.TimestampDisplay} [{entry.LevelDisplay}] {entry.CategoryDisplay} {entry.EventDisplay} {FormatPlainTextMessage(entry.Segments, markdownCompatible)}");
		}

		return builder.ToString().TrimEnd();
	}

	private static string FormatApplicationLogRow(InMemoryLogEntry entry)
	{
		return $"| {EscapeMarkdownTableCell(entry.TimestampDisplay)} | {EscapeMarkdownTableCell(entry.LevelDisplay)} | {EscapeMarkdownTableCell(entry.CategoryDisplay)} | {EscapeMarkdownTableCell(entry.EventDisplay)} | {FormatMarkdownMessage(entry.Segments)} |";
	}

	private static string FormatMarkdownMessage(IReadOnlyList<InMemoryLogSegment> segments)
	{
		var builder = new StringBuilder();
		foreach (var segment in segments)
		{
			var text = EscapeMarkdownTableCell(segment.Text);
			if (segment.IsValue)
			{
				var inline = text.Replace("`", "\\`");
				builder.Append('`').Append(inline).Append('`');
			}
			else
			{
				builder.Append(text);
			}
		}

		return builder.ToString();
	}

	private static string FormatPlainTextMessage(IReadOnlyList<InMemoryLogSegment> segments, bool markdownCompatible)
	{
		var builder = new StringBuilder();
		foreach (var segment in segments)
		{
			if (segment.IsValue && markdownCompatible)
			{
				builder.Append('`').Append(segment.Text.Replace("`", "\\`")).Append('`');
			}
			else
			{
				builder.Append(segment.Text);
			}
		}

		return builder.ToString();
	}

	private static string EscapeMarkdownTableCell(string text) =>
		text
			.Replace("\\", @"\\")
			.Replace("|", "\\|")
			.Replace("\r\n", "<br/>")
			.Replace("\n", "<br/>");

	private static bool PruneJsonNode(JsonNode? node)
	{
		switch (node)
		{
			case null:
				return false;
			case JsonObject obj:
			{
				var enumerable = obj.ToList().Where(pair => !PruneJsonNode(pair.Value));
				foreach (var pair in enumerable)
				{
					obj.Remove(pair.Key);
				}

				return obj.Count > 0;
			}
			case JsonArray array:
			{
				for (var i = array.Count - 1; i >= 0; i--)
				{
					if (!PruneJsonNode(array[i]))
					{
						array.RemoveAt(i);
					}
				}

				return true;
			}
		}

		return node switch
		{
			JsonValue value when value.TryGetValue<bool>(out var booleanValue) => booleanValue,
			JsonValue value when value.TryGetValue<string>(out var stringValue) => !string.IsNullOrEmpty(stringValue),
			JsonValue value when value.TryGetValue<int>(out var intValue) => intValue != 0,
			JsonValue value when value.TryGetValue<long>(out var longValue) => longValue != 0,
			JsonValue value when value.TryGetValue<double>(out var doubleValue) => Math.Abs(doubleValue) > double.Epsilon,
			JsonValue value when value.TryGetValue<decimal>(out var decimalValue) => decimalValue != 0,
			_ => true
		};
	}

	private static JsonObject CompactJsonForLength(JsonObject source, int maxCharacters, JsonSerializerOptions options)
	{
		var working = source.DeepClone().AsObject();
		working["_Truncated"] = "settings dump shortened for GitHub prefill";
		while (working.ToJsonString(options).Length > maxCharacters)
		{
			if (!TryShortenJsonNode(working))
			{
				return new JsonObject
				{
					["_Truncated"] = "settings dump shortened for GitHub prefill"
				};
			}
		}

		return working;
	}

	private static bool TryShortenJsonNode(JsonNode? node)
	{
		switch (node)
		{
			case JsonObject obj:
			{
				foreach (var property in obj.ToList().Where(p => p.Key != "_Truncated").Reverse())
				{
					switch (property.Value)
					{
						case JsonArray { Count: > 0 } array:
						{
							array.RemoveAt(array.Count - 1);
							if (array.Count == 0)
							{
								obj.Remove(property.Key);
							}

							return true;
						}
						case JsonObject childObject when TryShortenJsonNode(childObject):
						{
							if (childObject.Count == 0)
							{
								obj.Remove(property.Key);
							}

							return true;
						}
					}
				}

				var removable = obj.ToList().Select(property => property.Key).LastOrDefault(key => key != "_Truncated");
				if (removable == null)
				{
					return false;
				}

				obj.Remove(removable);
				return true;

			}
			case JsonArray { Count: > 0 } arr:
				arr.RemoveAt(arr.Count - 1);
				return true;
			default:
				return false;
		}
	}

	private static string GetApplicationVersion()
	{
		var assembly = Assembly.GetEntryAssembly() ?? typeof(FeedbackViewModel).Assembly;
		return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
			?? assembly.GetName().Version?.ToString()
			?? "unknown";
	}

	private static void TrySetClipboardText(string text)
	{
		try
		{
			var thread = new Thread(() =>
			{
				Clipboard.SetText(text);
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
		}
		catch
		{
			// ignored
		}
	}

#if DEBUG
	private static InMemoryLogBuffer CreateDesignTimeLogBuffer()
	{
		var buffer = new InMemoryLogBuffer(200);
		var now = DateTimeOffset.Now;
		buffer.Append(now.AddSeconds(-24), "BrowserPicker.App", LogLevel.Debug, new EventId(1001, "LogApplicationLaunched"),
			"Application launched with arguments: https://github.com/mortenn/BrowserPicker",
			[
				new InMemoryLogSegment("Application launched with arguments: ", false),
				new InMemoryLogSegment("https://github.com/mortenn/BrowserPicker", true)
			], null);
		buffer.Append(now.AddSeconds(-19), "BrowserPicker.UrlHandler", LogLevel.Information, new EventId(1012, "LogFaviconLoaded"),
			"Favicon successfully loaded from URL: https://github.com/favicon.ico",
			[
				new InMemoryLogSegment("Favicon successfully loaded from URL: ", false),
				new InMemoryLogSegment("https://github.com/favicon.ico", true)
			], null);
		buffer.Append(now.AddSeconds(-11), "BrowserPicker.ViewModel.ApplicationViewModel", LogLevel.Information, new EventId(1020, "LogAutomationMatchesFound"),
			"2 configured defaults match the url",
			[
				new InMemoryLogSegment("", false),
				new InMemoryLogSegment("2", true),
				new InMemoryLogSegment(" configured defaults match the url", false)
			], null);
		buffer.Append(now.AddSeconds(-6), "BrowserPicker.ViewModel.ApplicationViewModel", LogLevel.Warning, new EventId(1104, "LogAutomationFallback"),
			"No running browser matched; falling back to user choice",
			[new InMemoryLogSegment("No running browser matched; falling back to user choice", false)], null);
		buffer.Append(now.AddSeconds(-2), "BrowserPicker.Windows.JsonAppSettings", LogLevel.Error, new EventId(1203, "LogImportFailed"),
			"Unable to import configuration from the clipboard: expected a JSON object.",
			[new InMemoryLogSegment("Unable to import configuration from the clipboard: expected a JSON object.", false)], null);
		return buffer;
	}

	private sealed class DesignTimeSettings : IBrowserPickerConfiguration
	{
		public bool FirstTime { get; set; }
		public bool AlwaysPrompt { get; set; } = true;
		public bool AlwaysUseDefaults { get; set; } = true;
		public bool AlwaysAskWithoutDefault { get; set; }
		public int UrlLookupTimeoutMilliseconds { get; set; } = 2000;
		public SerializableSettings.SortOrder SortBy { get; set; } = SerializableSettings.SortOrder.Automatic;
		public bool UseManualOrdering { get => SortBy == SerializableSettings.SortOrder.Manual; set { if (value) SortBy = SerializableSettings.SortOrder.Manual; } }
		public bool UseAutomaticOrdering { get => SortBy == SerializableSettings.SortOrder.Automatic; set { if (value) SortBy = SerializableSettings.SortOrder.Automatic; } }
		public bool UseAlphabeticalOrdering { get => SortBy == SerializableSettings.SortOrder.Alphabetical; set { if (value) SortBy = SerializableSettings.SortOrder.Alphabetical; } }
		public bool DisableTransparency { get; set; } = true;
		public double WindowOpacity { get; set; } = 0.92;
		public bool DisableNetworkAccess { get; set; }
		public string[] UrlShorteners { get; set; } = [.. UrlHandler.DefaultUrlShorteners, "example.com"];
		public List<BrowserModel> BrowserList { get; } =
		[
			new(Firefox.Instance, null, string.Empty)
			{
				Usage = 8,
				Profiles =
				{
					new BrowserProfile("container:Work", "Work", null, "ext+container:name=Work&url={url}")
						{ IconColor = "orange", ContainerIcon = "briefcase" },
					new BrowserProfile("container:Personal", "Personal", null, "ext+container:name=Personal&url={url}")
						{ IconColor = "blue", ContainerIcon = "fingerprint" }
				}
			},
			new(Edge.Instance, null, string.Empty) { Usage = 3, Disabled = true },
			new(Chrome.Instance, null, string.Empty)
			{
				Usage = 5,
				Profiles =
				{
					new BrowserProfile("Default", "Personal", """--profile-directory="Default" """),
					new BrowserProfile("Profile 1", "Work", """--profile-directory="Profile 1" """)
				}
			}
		];
		public List<DefaultSetting> Defaults { get; } =
		[
			new(MatchType.Hostname, "github.com", Firefox.Instance.Name),
			new(MatchType.Default, string.Empty, Firefox.Instance.Name)
		];
		public List<KeyBinding> KeyBindings { get; } = [];
		public bool AutoSizeWindow { get; set; } = true;
		public double WindowWidth { get; set; }
		public double WindowHeight { get; set; }
		public double ConfigWindowWidth { get; set; } = 900;
		public double ConfigWindowHeight { get; set; } = 640;
		public double FontSize { get; set; } = 14;
		public ThemeMode ThemeMode { get; set; } = ThemeMode.Dark;
		public ProfileDisplayMode ProfileDisplayMode { get; set; }
		public bool UseFallbackDefault { get; set; } = true;
		public string? DefaultBrowser { get; set; } = Firefox.Instance.Name;
		public string BackupLog => "Copied configuration JSON to the clipboard.";
		public IComparer<BrowserModel>? BrowserSorter => null;
		public event PropertyChangedEventHandler? PropertyChanged;

		public void AddBrowser(BrowserModel browser)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BrowserList)));
		}

		public void PersistBrowser(BrowserModel _)
		{
		}

		public void FindBrowsers()
		{
		}

		public void AddDefault(MatchType matchType, string pattern, string browser, string? profile = null)
		{
		}

		public Task SaveAsync(string fileName) => Task.CompletedTask;

		public Task LoadAsync(string fileName) => Task.CompletedTask;

		public Task Start(CancellationToken cancellationToken) => Task.CompletedTask;
	}
#endif
}
