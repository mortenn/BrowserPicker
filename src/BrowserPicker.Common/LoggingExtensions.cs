using System;
using System.Net;
using Microsoft.Extensions.Logging;

namespace BrowserPicker.Common;

/// <summary>
/// Source-generated logging extension methods for BrowserPicker components.
/// </summary>
public static partial class LoggingExtensions
{
	[LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "Application launched with arguments: {Args}")]
	public static partial void LogApplicationLaunched(this ILogger logger, string args);

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

	[LoggerMessage(EventId = 1020, Level = LogLevel.Information, Message = "{Count} configured defaults match the URL")]
	public static partial void LogAutomationMatchesFound(this ILogger logger, int count);

	[LoggerMessage(EventId = 1021, Level = LogLevel.Debug,
		Message = "Automation inputs: Url={Url}, AlwaysPrompt={AlwaysPrompt}, AlwaysUseDefaults={AlwaysUseDefaults}, AlwaysAskWithoutDefault={AlwaysAskWithoutDefault}, UseFallbackDefault={UseFallbackDefault}, FallbackBrowser={FallbackBrowser}")]
	public static partial void LogAutomationInputs(this ILogger logger, string? url, bool alwaysPrompt,
		bool alwaysUseDefaults, bool alwaysAskWithoutDefault, bool useFallbackDefault, string? fallbackBrowser);

	[LoggerMessage(EventId = 1022, Level = LogLevel.Debug,
		Message = "Default candidate: Type={MatchType}, Pattern={Pattern}, Browser={Browser}, MatchLength={MatchLength}")]
	public static partial void LogAutomationMatchCandidate(this ILogger logger, string matchType, string? pattern,
		string? browser, int matchLength);

	[LoggerMessage(EventId = 1023, Level = LogLevel.Debug, Message = "Chosen default browser key: {MatchedKey} ({Source})")]
	public static partial void LogAutomationMatchedKey(this ILogger logger, string? matchedKey, string source);

	[LoggerMessage(EventId = 1024, Level = LogLevel.Debug,
		Message = "Resolved browser key {MatchedKey} to browser Id={BrowserId}, Name={BrowserName}, Disabled={Disabled}, Removed={Removed}")]
	public static partial void LogAutomationResolvedBrowser(this ILogger logger, string? matchedKey, string? browserId,
		string? browserName, bool? disabled, bool? removed);

	[LoggerMessage(EventId = 1025, Level = LogLevel.Debug,
		Message = "Configured browser {BrowserName} will be used. AlwaysUseDefaults={AlwaysUseDefaults}, IsRunning={IsRunning}")]
	public static partial void LogAutomationUsingConfiguredBrowser(this ILogger logger, string? browserName,
		bool alwaysUseDefaults, bool? isRunning);

	[LoggerMessage(EventId = 1026, Level = LogLevel.Debug,
		Message = "Configured browser {BrowserName} will not be used. AlwaysUseDefaults={AlwaysUseDefaults}, IsRunning={IsRunning}")]
	public static partial void LogAutomationSkippingConfiguredBrowser(this ILogger logger, string? browserName,
		bool alwaysUseDefaults, bool? isRunning);

	[LoggerMessage(EventId = 1027, Level = LogLevel.Debug, Message = "Running browsers considered for fallback: {Browsers}")]
	public static partial void LogAutomationRunningBrowsers(this ILogger logger, string browsers);

	[LoggerMessage(EventId = 1028, Level = LogLevel.Debug, Message = "Returning single running browser fallback: {BrowserName}")]
	public static partial void LogAutomationSingleRunningBrowser(this ILogger logger, string? browserName);

	[LoggerMessage(EventId = 1029, Level = LogLevel.Debug, Message = "Target URL is not a valid absolute Uri: {Url}")]
	public static partial void LogAutomationInvalidUrl(this ILogger logger, string? url);
}