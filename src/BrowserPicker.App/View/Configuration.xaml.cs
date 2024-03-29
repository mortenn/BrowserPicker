﻿using System.Windows;
using System.Windows.Controls;

namespace BrowserPicker.View
{
	/// <summary>
	/// Interaction logic for Configuration.xaml
	/// </summary>
	public partial class Configuration
	{
		public Configuration()
		{
			InitializeComponent();
		}

		private void AddDefault(object sender, RoutedEventArgs e)
		{
			var fragment = NewFragment.Text;
			var browser = (string)NewDefault.SelectedValue;
			DefaultsList.Items.Add(App.Settings.AddDefault(fragment, browser));
			NewFragment.Text = string.Empty;
			NewFragment.Focus();
			DefaultsList.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.UpdateTarget();
		}
	}
}
