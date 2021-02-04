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
			var rule = fragment.Split('|');
			if (rule.Length == 3)
			{
				MatchType = Enum.TryParse<MatchType>(rule[1], true, out var match) ? match : MatchType.Hostname;
				pattern = rule[2];
			}
			else
			{
				match_type = MatchType.Hostname;
				pattern = fragment;
				Save();
			}
			this.browser = browser;
		}
		
		public MatchType MatchType
		{
			get => match_type;
			set
			{
				match_type = value;
				OnPropertyChanged(nameof(MatchType));
				Save();
			}
		}

		public string Pattern
		{
			get => pattern;
			set
			{
				pattern = value;
				OnPropertyChanged(nameof(Pattern));
				Save();
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
				OnPropertyChanged();
				Save();
			}
		}

		public int MatchLength(Uri url)
		{
			switch (MatchType)
			{
				case MatchType.Hostname:
					return url.Host.EndsWith(Pattern) ? Pattern.Length : 0;

				case MatchType.Prefix:
					return url.OriginalString.StartsWith(Pattern) ? Pattern.Length : 0;

				case MatchType.Regex:
					return Regex.Match(url.OriginalString, Pattern).Length;

				default:
					return 0;
			}
		}

		public DelegateCommand Remove => new DelegateCommand(() => Pattern = string.Empty);

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private void Save()
		{
			Config.SetDefault($"|{MatchType}|{Pattern}", browser);
		}

		private string browser;
		private string pattern;
		private MatchType match_type;
	}
}
