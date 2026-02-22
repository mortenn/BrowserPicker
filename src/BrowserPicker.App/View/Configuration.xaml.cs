using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

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
		if (App.Settings != null && App.Settings is INotifyPropertyChanged inpc)
			inpc.PropertyChanged += Settings_PropertyChanged;
	}

	private void Configuration_Loaded(object sender, RoutedEventArgs e)
	{
		ApplyContentTheme();
	}

	private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(BrowserPicker.IApplicationSettings.ThemeMode))
			return;
		ApplyContentTheme();
	}

	private void ApplyContentTheme()
	{
		App.GetContentThemeBrushes(out var background, out var foreground);
		Background = background;
		Foreground = foreground;
	}

	private void CheckBox_Checked(object sender, System.Windows.RoutedEventArgs e)
	{

	}
}
