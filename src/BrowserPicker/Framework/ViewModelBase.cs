namespace BrowserPicker.Framework
{
	public abstract class ViewModelBase<T>(T model) : ModelBase where T : ModelBase
	{
		public T Model { get; } = model;
	}
}