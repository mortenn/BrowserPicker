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
			var forceChoice = false;
			if(arguments.Length > 1 && arguments[1] == "/choose")
			{
				TargetURL = arguments[2];
				forceChoice = true;
			}
			else
				TargetURL = arguments.Length > 1 ? arguments[1] : null;
			AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
			ViewModel = new ViewModel(forceChoice);
			Deactivated += (sender, args) => ViewModel.OnDeactivated();
		}

		private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
		{
			var e = (Exception) unhandledExceptionEventArgs.ExceptionObject;
			while(e != null)
			{
				MessageBox.Show(e.Message + e.StackTrace);
				e = e.InnerException;
			}
		}

		public static string TargetURL { get; private set; } = "https://github.com"; // Design time default

		public ViewModel ViewModel { get; }
	}
}
