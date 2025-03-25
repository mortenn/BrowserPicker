using System;
using System.Windows;
using BrowserPicker.View;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BrowserPicker;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		var host = Host.CreateDefaultBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton<App>();
				services.AddSingleton<MainWindow>();
			})
			.Build();


		var app = host.Services.GetRequiredService<App>();

		var resourceDictionary = new ResourceDictionary
		{
			Source = new Uri(
				"pack://application:,,,/BrowserPicker;component/Resources/ResourceDictionary.xaml",
				UriKind.Absolute
			)
		};
		app.Resources.MergedDictionaries.Add(resourceDictionary);

		app.Run();
	}
}
