using System;
using System.Windows;
using BrowserPicker.Common;
using BrowserPicker.UI.Views;
using BrowserPicker.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BrowserPicker.UI;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		// WPF's font subsystem (MS.Internal.FontCache.Util) builds the Windows Fonts path from the
		// "windir" environment variable. Some hosts (e.g. the Codex desktop app launching the Azure
		// DevOps MCP auth flow) start BrowserPicker with a stripped environment where "windir" is
		// missing, which makes WPF throw UriFormatException during startup before any window loads.
		// Restore it from SystemRoot / the Windows special folder so WPF can initialize. See issue #299.
		WindowsEnvironment.EnsureWindowsDirectory();

		var runtimeLogBuffer = new InMemoryLogBuffer();

		var builder = Host.CreateDefaultBuilder()
			// appsettings.json is only read at startup; skip the file watcher the default host sets up.
			.ConfigureHostConfiguration(config =>
				config.AddInMemoryCollection([new("hostBuilder:reloadConfigOnChange", "false")])
			)
			.ConfigureServices(services =>
			{
				services.AddSingleton(runtimeLogBuffer);
				services.AddSingleton<App>();
				services.AddSingleton<MainWindow>();
				services.AddSingleton<AppSettings>();
				// Active config is always JSON-backed so all changes are persisted to the JSON file.
				// When the JSON file does not exist, migrate from registry (read-only) then use JSON from then on.
				services.AddSingleton<IBrowserPickerConfiguration>(sp =>
				{
					var logger = sp.GetRequiredService<ILogger<JsonAppSettings>>();
					return JsonAppSettings.SettingsFileExists()
						? new JsonAppSettings(logger)
						: new JsonAppSettings(logger, sp.GetRequiredService<AppSettings>());
				});
			})
			.ConfigureLogging(logging =>
			{
				// Drop the default Console, Debug, EventSource and EventLog providers. A WinExe has no console,
				// and EventLog is added below with our own source name. Each provider costs startup time.
				logging.ClearProviders();
#if DEBUG
				logging.AddDebug();
#endif
				logging.AddProvider(new InMemoryLoggerProvider(runtimeLogBuffer));
				logging.AddEventLog(settings => settings.SourceName = "BrowserPicker");
			});

		var host = builder.Build();
		App.Services = host.Services;
		var configuration = host.Services.GetRequiredService<IBrowserPickerConfiguration>();
		App.Settings = configuration;
		var app = host.Services.GetRequiredService<App>();

		// Theme and app resources are loaded by App.EnsureThemeResources when the first window opens,
		// so a rule that launches a browser straight away does not pay for them.
		var logger = host.Services.GetRequiredService<ILogger<App>>();
		logger.LogApplicationLaunched(args.Length == 0 ? "(none)" : string.Join(" ", args));
#if !DEBUG && BROWSERPICKER_PORTABLE
		var registrationResult = UserDefaultBrowserRegistration.RegisterForCurrentUserIfPortableRelease(
			out var registrationDetail
		);
		logger.LogInformation(
			"Portable browser registration check completed with {Result}. {Detail}",
			registrationResult,
			registrationDetail ?? string.Empty
		);
#endif

		app.Run();
	}
}
