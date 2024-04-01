using JetBrains.Annotations;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BrowserPicker.Framework;

public abstract class ModelBase : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler PropertyChanged;

	[NotifyPropertyChangedInvocator]
	protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null)
	{
		if (Equals(field, newValue))
		{
			return false;
		}

		field = newValue;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		return true;
	}
}