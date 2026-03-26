using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using BrowserPicker.Common;
using BrowserPicker.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BrowserPicker.UI.Views;

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
		EnsureFeedbackViewModelLoaded();
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

	private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!ReferenceEquals(e.OriginalSource, Tabs))
		{
			return;
		}

		EnsureFeedbackViewModelLoaded();
	}

	private void EnsureFeedbackViewModelLoaded()
	{
		var hasLocalFeedbackDataContext =
			FeedbackContentRoot.ReadLocalValue(DataContextProperty) != DependencyProperty.UnsetValue;
		if (
			!FeedbackTab.IsSelected
			|| hasLocalFeedbackDataContext
			|| DataContext is not ConfigurationViewModel viewModel
		)
		{
			return;
		}

		FeedbackContentRoot.DataContext = new FeedbackViewModel(
			viewModel.Settings,
			App.Services.GetService<InMemoryLogBuffer>()
		);
	}
}
