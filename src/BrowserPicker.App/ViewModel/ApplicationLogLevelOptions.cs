using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace BrowserPicker.ViewModel;

public sealed record ApplicationLogLevelOption(string Label, LogLevel MinimumLevel);

public static class ApplicationLogLevelOptions
{
	public static IReadOnlyList<ApplicationLogLevelOption> All { get; } =
	[
		new("Everything", LogLevel.Trace),
		new("Debug and above", LogLevel.Debug),
		new("Information and above", LogLevel.Information),
		new("Warning and above", LogLevel.Warning),
		new("Error and above", LogLevel.Error),
		new("Critical only", LogLevel.Critical)
	];
}
