namespace BrowserPicker.Framework
{
	public abstract class ViewModelBase<T> : ModelBase where T : ModelBase
	{
		protected ViewModelBase(T model)
		{
			Model = model;
		}

		public T Model { get; }
	}
}