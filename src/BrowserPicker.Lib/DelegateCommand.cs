using System;
using System.Threading.Tasks;
using System.Windows.Input;
using JetBrains.Annotations;

namespace BrowserPicker.Lib
{
	[PublicAPI]
	public class DelegateCommand : ICommand
	{
		public event EventHandler CanExecuteChanged;

		protected DelegateCommand() { }

		public DelegateCommand(Action callback, Func<bool> canExecute = null)
		{
			execute = callback;
			can_execute = canExecute;
		}

		public DelegateCommand(Func<Task> callback, Func<bool> canExecute = null)
		{
			execute = () => callback();
			can_execute = canExecute;
		}

		public virtual bool CanExecute(object parameter)
		{
			return can_execute == null || can_execute();
		}

		public virtual void Execute(object parameter)
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

	[PublicAPI]
	public class DelegateCommand<T> : DelegateCommand
	{
		public DelegateCommand(Action<T> callback, Func<T, bool> canExecute = null)
		{
			execute = callback;
			can_execute = canExecute;
		}

		public DelegateCommand(Func<T, Task> callback, Func<T, bool> canExecute = null)
		{
			execute = argument => callback(argument);
			can_execute = canExecute;
		}

		public override bool CanExecute(object parameter)
		{
			return can_execute((T)parameter);
		}

		public override void Execute(object parameter)
		{
			execute((T)parameter);
		}

		private readonly Action<T> execute;
		private readonly Func<T, bool> can_execute;
	}
}