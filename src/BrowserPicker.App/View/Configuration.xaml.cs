using System.ComponentModel;
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
		Loaded += Configuration_Loaded;
		if (App.Settings is INotifyPropertyChanged setting)
			setting.PropertyChanged += Settings_PropertyChanged;
	}

	private void Configuration_Loaded(object sender, RoutedEventArgs e)
	{
		ApplyContentTheme();
	}

	private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(IApplicationSettings.ThemeMode))
			return;
		ApplyContentTheme();
	}

	private void ApplyContentTheme()
	{
		App.GetContentThemeBrushes(out var background, out var foreground);
		Background = background;
		Foreground = foreground;
	}
}
