using System;
using System.Windows.Input;
using JetBrains.Annotations;

namespace BrowserPicker.Framework;

public abstract class DelegateCommandBase : ICommand
{
	public event EventHandler? CanExecuteChanged;

	public abstract bool CanExecute(object? parameter);
		
	public abstract void Execute(object? parameter);

	public void RaiseCanExecuteChanged()
	{
		CanExecuteChanged?.Invoke(this, EventArgs.Empty);
	}
}

[PublicAPI]
public sealed class DelegateCommand(Action callback, Func<bool>? canExecute = null) : DelegateCommandBase
{
	public override bool CanExecute(object? parameter)
	{
		return canExecute?.Invoke() ?? true;
	}

	public override void Execute(object? parameter)
	{
		callback();
	}
}

[PublicAPI]
public sealed class DelegateCommand<T>(Action<T?> callback, Func<T?, bool>? canExecute = null) : DelegateCommandBase where T : class
{
	public override bool CanExecute(object? parameter)
	{
		return canExecute?.Invoke(parameter as T) ?? true;
	}

	public override void Execute(object? parameter)
	{
		callback(parameter as T);
	}
}