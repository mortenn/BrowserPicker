using System;
using System.Windows.Input;
using JetBrains.Annotations;

namespace BrowserPicker.Framework;

/// <summary>
/// Base class for command implementations that delegate to callbacks.
/// </summary>
public abstract class DelegateCommandBase : ICommand
{
	/// <inheritdoc />
	public event EventHandler? CanExecuteChanged;

	/// <inheritdoc />
	public abstract bool CanExecute(object? parameter);

	/// <inheritdoc />
	public abstract void Execute(object? parameter);

	/// <summary>
	/// Raises <see cref="CanExecuteChanged"/> so the UI re-evaluates <see cref="CanExecute"/>.
	/// </summary>
	public void RaiseCanExecuteChanged()
	{
		CanExecuteChanged?.Invoke(this, EventArgs.Empty);
	}
}

/// <summary>
/// Parameterless command that delegates to an action and optional can-execute predicate.
/// </summary>
/// <param name="callback">The action to execute.</param>
/// <param name="canExecute">Optional predicate; when null, the command is always executable.</param>
[PublicAPI]
public sealed class DelegateCommand(Action callback, Func<bool>? canExecute = null) : DelegateCommandBase
{
	/// <inheritdoc />
	public override bool CanExecute(object? parameter)
	{
		return canExecute?.Invoke() ?? true;
	}

	public override void Execute(object? parameter)
	{
		callback();
	}
}

/// <summary>
/// Command that takes a single parameter and delegates to an action and optional can-execute predicate.
/// </summary>
/// <typeparam name="T">The parameter type (reference type).</typeparam>
/// <param name="callback">The action to execute with the parameter.</param>
/// <param name="canExecute">Optional predicate; when null, the command is always executable.</param>
[PublicAPI]
public sealed class DelegateCommand<T>(Action<T?> callback, Func<T?, bool>? canExecute = null) : DelegateCommandBase where T : class
{
	/// <inheritdoc />
	public override bool CanExecute(object? parameter)
	{
		return canExecute?.Invoke(parameter as T) ?? true;
	}

	public override void Execute(object? parameter)
	{
		callback(parameter as T);
	}
}