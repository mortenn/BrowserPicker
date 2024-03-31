using BrowserPicker.Framework;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace BrowserPicker
{
	public class DefaultSetting(MatchType type, string pattern, string browser) : ModelBase, INotifyPropertyChanging
	{
		private string browser = browser;
		private string pattern = pattern;
		private MatchType type = type;

		public static DefaultSetting Decode(string rule, string browser)
		{
			if (rule == string.Empty)
			{
				return new(MatchType.Default, string.Empty, browser);
			}

			if (rule == null)
			{
				return new(MatchType.Hostname, rule, browser);
			}

			var config = rule.Split('|');
			if (config.Length > 1 && config[0] == string.Empty)
			{
				if (config.Length != 3)
				{
					// Unknown format detected, ignore rule
					return null;
				}

				var prefix = rule[1..rule.IndexOf('|', 1)];
				if (!Enum.TryParse<MatchType>(prefix, true, out var matchType))
				{
					// Unsupported match type detected, ignore rule
					return null;
				}
				return new(matchType, config[2], browser);
			}

			// Original hostname match type
			return new(MatchType.Hostname, rule, browser);
		}

		public MatchType Type
		{
			get => type;
			set
			{
				if (type == value)
				{
					return;
				}
				OnPropertyChanging(nameof(SettingKey));
				type = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(SettingKey));
			}
		}

		public string SettingKey => ToString();

		public string SettingValue => Browser;

		public bool Deleted { get; set; } = false;

		public string Pattern
		{
			get => pattern;
			set
			{
				if (pattern == value)
				{
					return;
				}
				// Triger deletion of old registry key
				OnPropertyChanging(nameof(SettingKey));
				pattern = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(IsValid));
				OnPropertyChanged(nameof(SettingKey));
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
				OnPropertyChanged(nameof(SettingValue));
			}
		}

		public bool IsValid
		{
			get => !string.IsNullOrWhiteSpace(pattern) || pattern == string.Empty && Type == MatchType.Default;
		}

		public DelegateCommand Remove => new(() => { Deleted = true; OnPropertyChanged(nameof(Deleted)); });

		public int MatchLength(Uri url)
		{
			if (!IsValid)
			{
				return 0;
			}
			return Type switch
			{
				MatchType.Default => 1,
				MatchType.Hostname => url.Host.EndsWith(pattern) ? pattern.Length : 0,
				MatchType.Prefix => url.OriginalString.StartsWith(pattern) ? pattern.Length : 0,
				MatchType.Regex => Regex.Match(url.OriginalString, pattern).Length,
				_ => 0,
			};
		}

		public override string ToString() => Type switch
		{
			MatchType.Hostname => pattern,
			MatchType.Default => string.Empty,
			_ => $"|{Type}|{pattern}"
		};

		public override int GetHashCode() => Type.GetHashCode() ^ pattern.GetHashCode();

		private void OnPropertyChanging([CallerMemberName] string propertyName = null)
		{
			PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
		}

		public event PropertyChangingEventHandler PropertyChanging;
	}
}
