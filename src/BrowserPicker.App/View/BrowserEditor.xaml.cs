using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using BrowserPicker.ViewModel;
using Microsoft.Win32;

namespace BrowserPicker.View
{
	/// <summary>
	/// Interaction logic for BrowserEditor.xaml
	/// </summary>
	public partial class BrowserEditor
	{
		public BrowserEditor()
		{
			InitializeComponent();
		}

		private void BrowserEditor_OnLoaded(object sender, RoutedEventArgs e)
		{
			DataContext = new BrowserViewModel(new BrowserModel(), null);
		}

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
					Browser.Model.Name = name.FileDescription;
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
			var result = browser.ShowDialog(this);
			if (result == true)
				Browser.Model.IconPath = browser.FileName;
		}

		private BrowserViewModel Browser => DataContext as BrowserViewModel;

		public void DragWindow(object sender, MouseButtonEventArgs args)
		{
			DragMove();
		}
	}
}
