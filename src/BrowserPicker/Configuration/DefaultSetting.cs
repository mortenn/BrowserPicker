using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace BrowserPicker.Configuration
{
	public class DefaultSetting : INotifyPropertyChanged
	{
		public DefaultSetting(string fragment, string browser)
		{
			this.fragment = fragment;
			this.browser = browser;
		}
		/// <summary>
		/// If Fragment starts with a pipe (|) character then it is of the format
		///      |type|value
		/// Otherwise the Fragment is a suffix-match on the URL host name.
		/// Currently supported Fragment types:
		///      |prefix|https://example.com/test           This applies a prefix match to the full URL value
		///      |regex|https://.*\.example\.com/test		This applies a regex match to the full URL value
		/// </summary>
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
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private string fragment;
		private string browser;

		public int MatchLength(Uri url)
		{
			var matchType = MatchType.Hostname;
			var value = Fragment;

			if (Fragment?.Length == 0)
				return 0;

			if (Fragment[0] == '|')
			{
				Enum.TryParse(Fragment.Substring(1, Fragment.IndexOf('|', 1) - 1), true, out matchType);
				value = Fragment.Substring(Fragment.IndexOf('|', 1) + 1);
			}

			switch (matchType)
			{
				case MatchType.Hostname:
					return url.Host.EndsWith(Fragment) ? Fragment.Length : 0;
				
				case MatchType.Prefix:
					return url.OriginalString.StartsWith(value) ? value.Length : 0;
				
				case MatchType.Regex:
					return Regex.Match(url.OriginalString, value).Length;
				
				default:
					return 0;
			}
		}
	}
}
