using JetBrains.Annotations;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BrowserPicker.Framework;

/// <summary>
/// Base class for models that support property change notification.
/// </summary>
public abstract class ModelBase : INotifyPropertyChanged
{
	/// <inheritdoc />
	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>
	/// Raises <see cref="PropertyChanged"/> for the given property.
	/// </summary>
	/// <param name="propertyName">Name of the property; defaults to the caller member name.</param>
	[NotifyPropertyChangedInvocator]
	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	/// <summary>
	/// Sets the field and raises <see cref="PropertyChanged"/> if the value changed.
	/// </summary>
	/// <typeparam name="T">Type of the property.</typeparam>
	/// <param name="field">The backing field.</param>
	/// <param name="newValue">The new value.</param>
	/// <param name="propertyName">Name of the property; defaults to the caller member name.</param>
	/// <returns>True if the value was updated; false if it was unchanged.</returns>
	protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
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