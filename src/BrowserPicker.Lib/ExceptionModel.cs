using JetBrains.Annotations;
using System;

namespace BrowserPicker.Lib
{
	public class ExceptionModel : ModelBase
	{
		// WPF Designer
		[UsedImplicitly]
		public ExceptionModel()
		{
			Exception = new Exception("Test", new Exception("Test 2", new Exception("Test 3")));
		}

		public ExceptionModel(Exception exception)
		{
			Exception = exception;
		}

		public Exception Exception { get; }
	}
}
