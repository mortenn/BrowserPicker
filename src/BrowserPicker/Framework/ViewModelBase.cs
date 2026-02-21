namespace BrowserPicker.Framework;

/// <summary>
/// Base class for view models that wrap a single model instance.
/// </summary>
/// <typeparam name="T">The model type.</typeparam>
/// <param name="model">The model instance.</param>
public abstract class ViewModelBase<T>(T model) : ModelBase where T : ModelBase
{
	/// <summary>
	/// Gets the wrapped model.
	/// </summary>
	public T Model { get; } = model;
}