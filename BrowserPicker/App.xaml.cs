using System;
using System.Reflection;
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
			AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
			ViewModel = new ViewModel();
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

		public ViewModel ViewModel { get; }
	}
}
