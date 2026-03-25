using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using JetBrains.Annotations;

namespace BrowserPicker.UI;

/// <summary>
/// Keeps a list box tailed to the end while the user remains scrolled to the bottom.
/// </summary>
public static class AutoTailListBoxBehavior
{
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(AutoTailListBoxBehavior),
			new PropertyMetadata(false, OnIsEnabledChanged));

	private static readonly DependencyProperty ControllerProperty =
		DependencyProperty.RegisterAttached(
			"Controller",
			typeof(Controller),
			typeof(AutoTailListBoxBehavior));

	[UsedImplicitly]
	public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

	public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not ListBox listBox)
		{
			return;
		}

		if ((bool)e.NewValue)
		{
			var controller = new Controller(listBox);
			listBox.SetValue(ControllerProperty, controller);
			controller.Attach();
			return;
		}

		(listBox.GetValue(ControllerProperty) as Controller)?.Detach();
		listBox.ClearValue(ControllerProperty);
	}

	private sealed class Controller(ListBox listBox)
	{
		private ScrollViewer? scroll_viewer;
		private bool stick_to_bottom = true;

		public void Attach()
		{
			listBox.Loaded += ListBox_Loaded;
			listBox.Unloaded += ListBox_Unloaded;
			HookCollectionChanged();
		}

		public void Detach()
		{
			listBox.Loaded -= ListBox_Loaded;
			listBox.Unloaded -= ListBox_Unloaded;
			UnhookCollectionChanged();
			if (scroll_viewer != null)
			{
				scroll_viewer.ScrollChanged -= ScrollViewer_ScrollChanged;
			}
		}

		private void ListBox_Loaded(object sender, RoutedEventArgs e)
		{
			scroll_viewer = FindScrollViewer(listBox);
			if (scroll_viewer != null)
			{
				scroll_viewer.ScrollChanged -= ScrollViewer_ScrollChanged;
				scroll_viewer.ScrollChanged += ScrollViewer_ScrollChanged;
			}

			listBox.Dispatcher.BeginInvoke(ScrollToEnd, DispatcherPriority.Loaded);
		}

		private void ListBox_Unloaded(object sender, RoutedEventArgs e)
		{
			if (scroll_viewer == null)
			{
				return;
			}

			scroll_viewer.ScrollChanged -= ScrollViewer_ScrollChanged;
			scroll_viewer = null;
		}

		private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			stick_to_bottom = e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 1;
		}

		private void HookCollectionChanged()
		{
			if (listBox.Items is INotifyCollectionChanged changed)
			{
				changed.CollectionChanged += Items_CollectionChanged;
			}
		}

		private void UnhookCollectionChanged()
		{
			if (listBox.Items is INotifyCollectionChanged changed)
			{
				changed.CollectionChanged -= Items_CollectionChanged;
			}
		}

		private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (!stick_to_bottom)
			{
				return;
			}

			listBox.Dispatcher.BeginInvoke(ScrollToEnd, DispatcherPriority.Background);
		}

		private void ScrollToEnd()
		{
			scroll_viewer?.ScrollToEnd();
			if (listBox.Items.Count == 0)
			{
				return;
			}

			var lastItem = listBox.Items[^1];
			if (lastItem != null)
			{
				listBox.ScrollIntoView(lastItem);
			}
		}

		private static ScrollViewer? FindScrollViewer(DependencyObject root)
		{
			if (root is ScrollViewer viewer)
			{
				return viewer;
			}

			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
			{
				var result = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
				if (result != null)
				{
					return result;
				}
			}

			return null;
		}
	}
}
