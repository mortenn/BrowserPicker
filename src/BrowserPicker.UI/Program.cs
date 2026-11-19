using System;
using System.Windows;
using BrowserPicker.Common;
using BrowserPicker.UI.Views;
using BrowserPicker.Windows;
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
		var diagnosticFileLog = new DiagnosticFileLog();

		var builder = Host.CreateDefaultBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton(runtimeLogBuffer);
				services.AddSingleton(diagnosticFileLog);
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
				logging.AddProvider(new InMemoryLoggerProvider(runtimeLogBuffer));
				logging.AddEventLog(settings => settings.SourceName = "BrowserPicker");
				// The file log decides itself what to keep (off unless enabled in settings), so let Debug through
				// regardless of the appsettings.json levels, which only lower the other providers.
				logging.AddProvider(new DiagnosticFileLoggerProvider(diagnosticFileLog));
				logging.AddFilter<DiagnosticFileLoggerProvider>(null, LogLevel.Debug);
			});

		var host = builder.Build();
		App.Services = host.Services;
		var configuration = host.Services.GetRequiredService<IBrowserPickerConfiguration>();
		App.Settings = configuration;
		ApplyDiagnosticLogSettings(diagnosticFileLog, configuration);
		configuration.PropertyChanged += (_, e) =>
		{
			if (
				e.PropertyName
				is nameof(IApplicationSettings.DiagnosticFileLogging)
					or nameof(IApplicationSettings.DiagnosticLogFullUrls)
					or nameof(IApplicationSettings.DiagnosticLogRetentionDays)
			)
			{
				ApplyDiagnosticLogSettings(diagnosticFileLog, configuration);
			}
		};
		var app = host.Services.GetRequiredService<App>();

		// Fluent theme (Windows 10/11) first, then app resources
		app.Resources.MergedDictionaries.Add(
			new ResourceDictionary
			{
				Source = new Uri(
					"pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml",
					UriKind.Absolute
				),
			}
		);
		app.Resources.MergedDictionaries.Add(
			new ResourceDictionary
			{
				Source = new Uri(
					"pack://application:,,,/BrowserPicker;component/Resources/ResourceDictionary.xaml",
					UriKind.Absolute
				),
			}
		);
		// Content theme brushes before Run() so DynamicResource resolves when windows load.
		app.AddContentThemeDictionary(configuration.ThemeMode);
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

	private static void ApplyDiagnosticLogSettings(DiagnosticFileLog log, IApplicationSettings settings) =>
		log.Configure(
			settings.DiagnosticFileLogging,
			settings.DiagnosticLogFullUrls,
			settings.DiagnosticLogRetentionDays
		);
}
