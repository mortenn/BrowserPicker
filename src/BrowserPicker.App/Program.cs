using System;
using System.Windows;
using BrowserPicker.View;
using BrowserPicker.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BrowserPicker;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		var builder = Host.CreateDefaultBuilder()
			.ConfigureServices(services =>
			{
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
			.ConfigureLogging(logging => logging.AddEventLog(
				settings => settings.SourceName = "BrowserPicker"
			));
		
		var host = builder.Build();
		App.Services = host.Services;
		App.Settings = host.Services.GetRequiredService<IBrowserPickerConfiguration>();
		var app = host.Services.GetRequiredService<App>();

		// Fluent theme (Windows 10/11) first, then app resources
		app.Resources.MergedDictionaries.Add(new ResourceDictionary
		{
			Source = new Uri("pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml", UriKind.Absolute)
		});
		app.Resources.MergedDictionaries.Add(new ResourceDictionary
		{
			Source = new Uri(
				"pack://application:,,,/BrowserPicker;component/Resources/ResourceDictionary.xaml",
				UriKind.Absolute
			)
		});
		// Content theme brushes before Run() so DynamicResource resolves when windows load.
		app.AddContentThemeDictionary(App.Settings.ThemeMode);
		var logger = host.Services.GetRequiredService<ILogger<App>>();
		logger.LogApplicationLaunched(args);

		app.Run();
	}
}
