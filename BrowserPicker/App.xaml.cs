using System.Security.Policy;
using System.Windows;

namespace BrowserPicker
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public App()
		{
			ViewModel = new ViewModel(false);
		}

		public ViewModel ViewModel { get; }
	}
}
