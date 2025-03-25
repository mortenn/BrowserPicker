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
			})
			.ConfigureLogging(logging => logging.AddEventLog(
				settings => settings.SourceName = "BrowserPicker"
			));
		
		var host = builder.Build();
		App.Services = host.Services;
		App.Settings = host.Services.GetRequiredService<AppSettings>();
		var app = host.Services.GetRequiredService<App>();

		var resourceDictionary = new ResourceDictionary
		{
			Source = new Uri(
				"pack://application:,,,/BrowserPicker;component/Resources/ResourceDictionary.xaml",
				UriKind.Absolute
			)
		};
		app.Resources.MergedDictionaries.Add(resourceDictionary);
		var logger = host.Services.GetRequiredService<ILogger<App>>();
		logger.LogApplicationLaunched(args);

		app.Run();
	}
}
