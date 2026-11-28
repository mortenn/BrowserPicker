using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using JetBrains.Annotations;

namespace BrowserPicker.UI;

/// <summary>
/// Writes a combo box selection to its binding source only when the user picks an item.
/// </summary>
/// <remarks>
/// <para>
/// Inside a <see cref="DataGrid"/> row, WPF holds a cell combo box's new value until the row edit is committed.
/// Picking an item from the drop-down does not start a row edit, so the choice was never saved.
/// </para>
/// <para>
/// Updating the source on every change is not safe either: when the item list is rebuilt (for example when
/// the browsers are re-sorted), the combo box clears its selection, and that empty value would overwrite the rule.
/// Bind <c>SelectedValue</c> or <c>SelectedItem</c> with <c>UpdateSourceTrigger=Explicit</c> and set
/// <c>IsEnabled</c> to true: the source is then updated only when an item is selected while the user is
/// working with the combo box (drop-down open or keyboard focus inside it).
/// </para>
/// </remarks>
public static class ComboBoxUserSelectionBehavior
{
	public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
		"IsEnabled",
		typeof(bool),
		typeof(ComboBoxUserSelectionBehavior),
		new PropertyMetadata(false, OnIsEnabledChanged)
	);

	[UsedImplicitly]
	public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

	public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not ComboBox comboBox)
		{
			return;
		}

		comboBox.SelectionChanged -= ComboBox_SelectionChanged;
		if ((bool)e.NewValue)
		{
			comboBox.SelectionChanged += ComboBox_SelectionChanged;
		}
	}

	private static void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (sender is not ComboBox comboBox)
		{
			return;
		}

		if (e.AddedItems.Count > 0 && (comboBox.IsDropDownOpen || comboBox.IsKeyboardFocusWithin))
		{
			comboBox.GetBindingExpression(Selector.SelectedValueProperty)?.UpdateSource();
			comboBox.GetBindingExpression(Selector.SelectedItemProperty)?.UpdateSource();
			return;
		}

		if (e.AddedItems.Count == 0)
		{
			// The item list changed under the selection. Show the stored value again once the list settles,
			// instead of keeping an empty selection that a later row commit would save over the rule.
			comboBox.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, () => RestoreFromSource(comboBox));
		}
	}

	private static void RestoreFromSource(ComboBox comboBox)
	{
		comboBox.GetBindingExpression(Selector.SelectedValueProperty)?.UpdateTarget();
		comboBox.GetBindingExpression(Selector.SelectedItemProperty)?.UpdateTarget();
	}
}
