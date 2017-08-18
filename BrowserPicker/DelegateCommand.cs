using System;
using System.Windows.Input;

namespace BrowserPicker
{
	public class DelegateCommand : ICommand
	{
		public event EventHandler CanExecuteChanged;

		public DelegateCommand(Action callback, Func<bool> canExecute = null)
		{
			execute = callback;
			can_execute = canExecute;
		}

		public bool CanExecute(object parameter)
		{
			return can_execute == null || can_execute();
		}

		public void Execute(object parameter)
		{
			execute();
		}

		public void RaiseCanExecuteChanged()
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}

		private readonly Action execute;
		private readonly Func<bool> can_execute;
	}
}