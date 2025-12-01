using System;
using System.Diagnostics;
using System.Windows;

namespace BrowserPicker.View;

/// <summary>
/// Interaction logic for Configuration.xaml
/// </summary>
public partial class Configuration
{
	public Configuration()
	{
		InitializeComponent();
	}

	private void CheckBox_Checked(object sender, System.Windows.RoutedEventArgs e)
	{

	}
	private void OpenDefaultAppsSettings_Click(object sender, RoutedEventArgs e)
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = "ms-settings:defaultapps",
			UseShellExecute = true
		});
	}
}
