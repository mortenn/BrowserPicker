using System;
using System.Text.RegularExpressions;

namespace BrowserPicker.Lib
{
	public class DefaultSetting : ModelBase
	{
		public DefaultSetting(string fragment, string browser)
		{
			this.fragment = fragment;
			this.browser = browser;
			Configure();
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
				{
					return;
				}
				// Trigger deletion if fragment is changing
				IsValid = !string.IsNullOrEmpty(fragment);

				// Skip adding to configuration if empty
				IsValid = string.IsNullOrWhiteSpace(value);
				fragment = value;
				Configure();
				OnPropertyChanged();
			}
		}

		public string Browser
		{
			get => browser;
			set
			{
				if (browser == value)
				{
					return;
				}
				browser = value;
				OnPropertyChanged();
			}
		}

		public bool IsValid
		{
			get => isValid;
			set
			{
				isValid = value;
				OnPropertyChanged();
			}
		}

		public DelegateCommand Remove => new DelegateCommand(() => Fragment = string.Empty);

		public int MatchLength(Uri url)
		{
			if (!IsValid)
			{
				return 0;
			}
			switch (type)
			{
				case MatchType.Hostname:
					return url.Host.EndsWith(pattern) ? pattern.Length : 0;

				case MatchType.Prefix:
					return url.OriginalString.StartsWith(pattern) ? pattern.Length : 0;

				case MatchType.Regex:
					return Regex.Match(url.OriginalString, pattern).Length;

				default:
					return 0;
			}
		}

		private void Configure()
		{
			pattern = null;
			if (!IsValid)
			{
				return;
			}
			var config = fragment.Split('|');
			if (config.Length > 1 && config[0] == string.Empty)
			{
				if (config.Length != 3)
				{
					// Unknown format detected, ignore rule
					return;
				}
				if (Enum.TryParse<MatchType>(fragment.Substring(1, fragment.IndexOf('|', 1) - 1), true, out var matchType))
				{
					type = matchType;
					pattern = config[2];
					return;
				}
				// Unsupported match type detected, ignore rule
				return;
			}

			// Default match type
			type = MatchType.Hostname;
			pattern = fragment;
		}

		private string fragment;
		private string browser;
		private MatchType type = MatchType.Hostname;
		private string pattern;
		private bool isValid;
	}
}
