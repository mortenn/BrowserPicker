using System;
using System.Windows;
using System.Windows.Input;

namespace BrowserPicker.UI.Views;

/// <summary>
/// The settings window. The picker opens it from "Configure browser list" and "Feedback", and it is the
/// only window when BrowserPicker starts without a URL.
/// </summary>
public partial class ConfigurationWindow
{
	public ConfigurationWindow()
	{
		InitializeComponent();
		if (App.Settings is not { } settings)
			return;
		if (settings.ConfigWindowWidth > 0)
			Width = Math.Max(MinWidth, settings.ConfigWindowWidth);
		if (settings.ConfigWindowHeight > 0)
			Height = Math.Max(MinHeight, settings.ConfigWindowHeight);
	}

	private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		// Remember the size for next time, but not a maximized or minimized one.
		if (WindowState != WindowState.Normal || App.Settings is not { } settings)
			return;
		settings.ConfigWindowWidth = Math.Max(MinWidth, e.NewSize.Width);
		settings.ConfigWindowHeight = Math.Max(MinHeight, e.NewSize.Height);
	}

	private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
	{
		if (e.Key != Key.Escape)
			return;
		e.Handled = true;
		Close();
	}
}
