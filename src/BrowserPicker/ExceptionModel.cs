using BrowserPicker.Framework;
using JetBrains.Annotations;
using System;

namespace BrowserPicker;

/// <summary>
/// Wraps an <see cref="Exception"/> for display in the exception report UI.
/// </summary>
public sealed class ExceptionModel(Exception exception) : ModelBase
{
	/// <summary>
	/// Parameterless constructor for WPF designer; uses a sample exception.
	/// </summary>
	[UsedImplicitly]
	public ExceptionModel() : this(new Exception("Test", new Exception("Test 2", new Exception("Test 3"))))
	{
	}

	/// <summary>
	/// The exception to display.
	/// </summary>
	public Exception Exception { get; } = exception;
}