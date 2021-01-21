using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace BrowserPicker
{
	public class DefaultSetting : INotifyPropertyChanged
	{
		private const string MatchTypePrefix = "prefix";
		private const string MatchTypeRegex = "regex";

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
			// Suffix match on the host name
			if (!Fragment.StartsWith("|"))
				return url.Host.EndsWith(Fragment) ? Fragment.Length : 0;

			var splitIndex = Fragment.IndexOf('|', 1);
			if (splitIndex <0 )
			{
				// bad format
				return 0;
			}
			
			var matchType = Fragment.Substring(1, splitIndex-1);
			var value = Fragment.Substring(splitIndex+1);
			switch (matchType)
			{
				case MatchTypePrefix:
					return url.OriginalString.StartsWith(value) ? value.Length : 0;
				case MatchTypeRegex:
					var match = Regex.Match(url.OriginalString, value);
					return match.Length;
				default:
					// unhandled match type
					return 0;
			}
		}
	}
}
