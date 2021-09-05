namespace BrowserPicker
{
	public abstract class ViewModelBase<T> : ModelBase where T : ModelBase
	{
		public ViewModelBase(T model)
		{
			Model = model;
		}

		public T Model { get; }
	}
}