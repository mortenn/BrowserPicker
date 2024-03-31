using System;
using System.Threading.Tasks;
using System.Windows.Input;
using JetBrains.Annotations;

namespace BrowserPicker.Framework
{
	public abstract class DelegateCommandBase : ICommand
	{
		protected DelegateCommandBase() { }

		public event EventHandler CanExecuteChanged;

		public abstract bool CanExecute(object parameter);
		
		public abstract void Execute(object parameter);

		public void RaiseCanExecuteChanged()
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	[PublicAPI]
	public sealed class DelegateCommand : DelegateCommandBase
	{
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

		public override bool CanExecute(object parameter)
		{
			return can_execute == null || can_execute();
		}

		public override void Execute(object parameter)
		{
			execute();
		}

		private readonly Action execute;
		private readonly Func<bool> can_execute;
	}

	[PublicAPI]
	public sealed class DelegateCommand<T> : DelegateCommandBase
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