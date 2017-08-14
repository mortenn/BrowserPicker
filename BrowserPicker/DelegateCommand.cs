using System;
using System.Windows.Input;

namespace BrowserPicker
{
	public class DelegateCommand : ICommand
	{
		private readonly Action execute;

		public event EventHandler CanExecuteChanged;

		public DelegateCommand(Action execute) 
		{
			this.execute = execute;
		}

		public bool CanExecute(object parameter)
		{
			return true;
		}

		public void Execute(object parameter)
		{
			execute();
		}

		public void RaiseCanExecuteChanged()
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}