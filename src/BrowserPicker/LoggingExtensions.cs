using System;
using System.Net;
using Microsoft.Extensions.Logging;

namespace BrowserPicker;

/// <summary>
/// Source-generated logging extension methods for BrowserPicker components.
/// </summary>
public static partial class LoggingExtensions
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "Application launched with arguments: {Args}")]
    public static partial void LogApplicationLaunched(this ILogger logger, string[] args);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Debug, Message = "Requested URL: {URL}")]
    public static partial void LogRequestedUrl(this ILogger logger, string? url);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Debug, Message = "Network access is disabled: {Flag}")]
    public static partial void LogNetworkAccessDisabled(this ILogger logger, bool flag);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "Browser added: {BrowserName}")]
    public static partial void LogBrowserAdded(this ILogger logger, string browserName);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information, Message = "Browser removed: {BrowserName}")]
    public static partial void LogBrowserRemoved(this ILogger logger, string browserName);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Information,
        Message = "Default setting added: MatchType - {MatchType}, Pattern - {Pattern}, Browser - {Browser}")]
    public static partial void LogDefaultSettingAdded(this ILogger logger, string matchType, string pattern,
        string? browser);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Debug, Message = "Jump URL detected: {JumpUrl}")]
    public static partial void LogJumpUrl(this ILogger logger, Uri jumpUrl);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Debug, Message = "Shortened URL detected: {ShortenedUrl}")]
    public static partial void LogShortenedUrl(this ILogger logger, string shortenedUrl);

    [LoggerMessage(EventId = 1009, Level = LogLevel.Debug, Message = "Failed to load favicon: {StatusCode}")]
    public static partial void LogFaviconFailed(this ILogger logger, HttpStatusCode statusCode);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Debug, Message = "Trying default favicon")]
    public static partial void LogDefaultFavicon(this ILogger logger);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Debug, Message = "Favicon could not be determined")]
    public static partial void LogFaviconNotFound(this ILogger logger);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Debug, Message = "Favicon successfully loaded from URL: {Url}")]
    public static partial void LogFaviconLoaded(this ILogger logger, string url);

    [LoggerMessage(EventId = 1013, Level = LogLevel.Debug, Message = "Favicon found with URL: {Url}")]
    public static partial void LogFaviconFound(this ILogger logger, string url);

    [LoggerMessage(EventId = 1014, Level = LogLevel.Debug, Message = "Lookup of configured defaults returned browser: {Choice}")]
    public static partial void LogAutomationChoice(this ILogger logger, string? choice);

    [LoggerMessage(EventId = 1015, Level = LogLevel.Debug, Message = "Configured to always ask for browser choice")]
    public static partial void LogAutomationAlwaysPrompt(this ILogger logger);

    [LoggerMessage(EventId = 1016, Level = LogLevel.Information, Message = "Browser {BrowserName} was selected, running is: {IsRunning}")]
    public static partial void LogAutomationBrowserSelected(this ILogger logger, string? browserName, bool? isRunning);

    [LoggerMessage(EventId = 1017, Level = LogLevel.Debug, Message = "No defaults found, always prompt enabled")]
    public static partial void LogAutomationAlwaysPromptWithoutDefaults(this ILogger logger);

    [LoggerMessage(EventId = 1018, Level = LogLevel.Debug, Message = "Found {Count} running browsers")]
    public static partial void LogAutomationRunningCount(this ILogger logger, int count);

    [LoggerMessage(EventId = 1019, Level = LogLevel.Debug, Message = "No browser defaults configured")]
    public static partial void LogAutomationNoDefaultsConfigured(this ILogger logger);

    [LoggerMessage(EventId = 1020, Level = LogLevel.Information, Message = "{Count} configured defaults match the url")]
    public static partial void LogAutomationMatchesFound(this ILogger logger, int count);
}