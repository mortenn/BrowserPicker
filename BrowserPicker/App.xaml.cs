using System;
using System.Windows;

namespace BrowserPicker
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App
	{
		public App()
		{
			var arguments = Environment.GetCommandLineArgs();
			TargetURL = arguments.Length > 1 ? arguments[1] : null;
			AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
			ViewModel = new ViewModel();
			Deactivated += (sender, args) => Current.Shutdown();
		}

		private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
		{
			var e = (Exception) unhandledExceptionEventArgs.ExceptionObject;
			while(e != null)
			{
				MessageBox.Show(e.Message + e.StackTrace);
				e = e.InnerException;
			}
		}

		public static string TargetURL { get; private set; }

		public ViewModel ViewModel { get; }
	}
}
