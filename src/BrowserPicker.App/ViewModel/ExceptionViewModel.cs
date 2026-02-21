using BrowserPicker.Framework;
using JetBrains.Annotations;
using System;
using System.Threading;
using System.Windows;

namespace BrowserPicker.ViewModel;

/// <summary>
/// View model for the exception report dialog: displays an exception and provides copy/close actions.
/// </summary>
public sealed class ExceptionViewModel : ViewModelBase<ExceptionModel>
{
	/// <summary>
	/// Parameterless constructor for WPF designer; uses a sample exception.
	/// </summary>
	[UsedImplicitly]
	public ExceptionViewModel() : base(new ExceptionModel(new Exception("Test", new Exception("Test 2", new Exception("Test 3")))))
	{
	}

	/// <summary>
	/// Initializes the view model with the exception to display.
	/// </summary>
	/// <param name="exception">The exception to show in the report.</param>
	public ExceptionViewModel(Exception exception) : base (new ExceptionModel(exception))
	{
		CopyToClipboard = new DelegateCommand(CopyExceptionDetailsToClipboard);
		Ok = new DelegateCommand(CloseWindow);
	}

	/// <summary>
	/// Command to copy the exception details to the clipboard.
	/// </summary>
	public DelegateCommand? CopyToClipboard { get; }
	/// <summary>
	/// Command to close the exception report window.
	/// </summary>
	public DelegateCommand? Ok { get; }

	/// <summary>
	/// Raised when the window is closed (e.g. after Ok).
	/// </summary>
	public EventHandler? OnWindowClosed;

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
