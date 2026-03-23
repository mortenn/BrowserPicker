using System.Windows;
using System.Windows.Input;

#if DEBUG
using JetBrains.Annotations;
#endif

namespace BrowserPicker.UI.Views;

/// <summary>
/// Interaction logic for UrlEditor.xaml
/// </summary>
public partial class UrlEditor
{
	public string? EditedUrl { get; set; }

#if DEBUG
	[UsedImplicitly]
	public UrlEditor()
	{
		InitializeComponent();
	}
#endif

	public UrlEditor(string? initialUrl)
	{
		EditedUrl = initialUrl;
		InitializeComponent();
	}

	private void Window_OnLoaded(object sender, RoutedEventArgs e)
	{
		EditorTextBox.Focus();
		EditorTextBox.SelectAll();
	}

	private void Save_OnClick(object sender, RoutedEventArgs e)
	{
		DialogResult = true;
	}

	private void Cancel_OnClick(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
	}

	private void Window_OnPreviewKeyUp(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			e.Handled = true;
			DialogResult = false;
			return;
		}

		if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.Control)
		{
			return;
		}

		e.Handled = true;
		DialogResult = true;
	}
}
