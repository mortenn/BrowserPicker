using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BrowserPicker.Annotations;

namespace BrowserPicker
{
	public class DefaultSetting : INotifyPropertyChanged
	{
		public DefaultSetting(string fragment, string browser)
		{
			this.fragment = fragment;
			this.browser = browser;
		}

		public string Fragment
		{
			get => fragment;
			set
			{
				if (fragment == value)
					return;

				if (!string.IsNullOrEmpty(fragment))
					Config.RemoveDefault(fragment);
				if (!string.IsNullOrEmpty(value))
					Config.SetDefault(value, Browser);
				fragment = value;
				OnPropertyChanged();
			}
		}

		public string Browser
		{
			get => browser;
			set
			{
				if (browser == value)
					return;

				browser = value;
				Config.SetDefault(Fragment, value);
				OnPropertyChanged();
			}
		}

		public DelegateCommand Remove => new DelegateCommand(() => Fragment = string.Empty);

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private string fragment;
		private string browser;
	}
}
