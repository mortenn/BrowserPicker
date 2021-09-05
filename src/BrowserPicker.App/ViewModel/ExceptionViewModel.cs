using BrowserPicker.Framework;
using JetBrains.Annotations;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace BrowserPicker.ViewModel
{
	public class ExceptionViewModel : ViewModelBase<ExceptionModel>
	{
		// WPF Designer
		[UsedImplicitly]
		public ExceptionViewModel() : base(new ExceptionModel(new Exception("Test", new Exception("Test 2", new Exception("Test 3")))))
		{
		}

		public ExceptionViewModel(Exception exception) : base (new ExceptionModel(exception))
		{
			CopyToClipboard = new DelegateCommand(CopyExceptionDetailsToClipboard);
			Ok = new DelegateCommand(CloseWindow);
		}

		public ICommand CopyToClipboard { get; }
		public ICommand Ok { get; }

		public EventHandler OnWindowClosed;

		private void CopyExceptionDetailsToClipboard()
		{
			try
			{
				var thread = new Thread(() => Clipboard.SetText(Model.Exception.ToString()));
				thread.SetApartmentState(ApartmentState.STA);
				thread.Start();
				thread.Join();
			}
			catch
			{
				// ignored
			}
		}


		private void CloseWindow()
		{
			OnWindowClosed?.Invoke(this, EventArgs.Empty);
		}
	}
}
