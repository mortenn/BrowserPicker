using BrowserPicker.Framework;
using JetBrains.Annotations;
using System;

namespace BrowserPicker;

public sealed class ExceptionModel(Exception exception) : ModelBase
{
	// WPF Designer
	[UsedImplicitly]
	public ExceptionModel() : this(new Exception("Test", new Exception("Test 2", new Exception("Test 3"))))
	{
	}

	public Exception Exception { get; } = exception;
}