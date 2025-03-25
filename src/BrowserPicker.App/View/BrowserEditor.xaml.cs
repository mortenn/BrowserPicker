using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using BrowserPicker.ViewModel;
using Microsoft.Win32;
#if DEBUG
using JetBrains.Annotations;
#endif

namespace BrowserPicker.View;

/// <summary>
/// Interaction logic for BrowserEditor.xaml
/// </summary>
public partial class BrowserEditor
{
#if DEBUG
	/// <summary>
	/// Design time constructor
	/// </summary>
	[UsedImplicitly]
	public BrowserEditor()
	{
		InitializeComponent();
		Browser = new BrowserViewModel();
		DataContext = Browser;
	}
#endif

	public BrowserEditor(BrowserViewModel viewModel)
	{
		InitializeComponent();
		Browser = viewModel;
		DataContext = Browser;
	}

	private BrowserViewModel Browser { get; }

	private void Ok_OnClick(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void Cancel_OnClick(object sender, RoutedEventArgs e)
	{
		DataContext = null;
		Close();
	}

	private void Command_Browse(object sender, RoutedEventArgs e)
	{
		var browser = new OpenFileDialog
		{
			DefaultExt = ".exe",
			Filter = "Executable Files (*.exe)|*.exe|All Files|*.*"
		};
		var result = browser.ShowDialog(this);
		if (result != true)
			return;

		Browser.Model.Command = browser.FileName;
		if (string.IsNullOrEmpty(Browser.Model.Name))
		{
			try
			{
				var name = FileVersionInfo.GetVersionInfo(browser.FileName);
				Browser.Model.Name = name.FileDescription ?? string.Empty;
			}
			catch
			{
				// ignored
			}
		}
		if (string.IsNullOrEmpty(Browser.Model.IconPath))
			Browser.Model.IconPath = browser.FileName;
	}

	private void Icon_Browse(object sender, RoutedEventArgs e)
	{
		var browser = new OpenFileDialog
		{
			DefaultExt = ".exe",
			Filter = "Executable Files (*.exe)|*.exe|Icon Files (*.ico)|*.ico|JPEG Files (*.jpeg)|*.jpeg|PNG Files (*.png)|*.png|JPG Files (*.jpg)|*.jpg|GIF Files (*.gif)|*.gif|All Files|*.*"
		};
		if (browser.ShowDialog(this) == true)
			Browser.Model.IconPath = browser.FileName;
	}


	private void DragWindow(object sender, MouseButtonEventArgs args)
	{
		DragMove();
	}
}
