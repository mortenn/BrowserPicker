using System.Collections.Generic;
using BrowserPicker.Common.Resources;
using Microsoft.Extensions.Logging;

namespace BrowserPicker.UI.ViewModels;

public sealed record ApplicationLogLevelOption(string Label, LogLevel MinimumLevel);

public static class ApplicationLogLevelOptions
{
	public static IReadOnlyList<ApplicationLogLevelOption> All { get; } =
	[
		new(Strings.LogLevel_Everything, LogLevel.Trace),
		new(Strings.LogLevel_DebugAndAbove, LogLevel.Debug),
		new(Strings.LogLevel_InformationAndAbove, LogLevel.Information),
		new(Strings.LogLevel_WarningAndAbove, LogLevel.Warning),
		new(Strings.LogLevel_ErrorAndAbove, LogLevel.Error),
		new(Strings.LogLevel_CriticalOnly, LogLevel.Critical),
	];
}
