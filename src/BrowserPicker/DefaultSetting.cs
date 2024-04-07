using BrowserPicker.Framework;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BrowserPicker;

public sealed class DefaultSetting(MatchType type, string? pattern, string? browser) : ModelBase, INotifyPropertyChanging
{
	private bool deleted;

	public static DefaultSetting? Decode(string? rule, string browser)
	{
		if (rule == null)
		{
			return new DefaultSetting(MatchType.Hostname, null, browser);
		}
		var config = rule.Split('|');
		return config.Length switch
		{
			// Default browser choice
			1 when rule == string.Empty => new DefaultSetting(MatchType.Default, string.Empty, browser),
			
			// Original hostname match type
			<= 1 => new DefaultSetting(MatchType.Hostname, rule, browser),
			
			// New configuration format using MatchType
			3 when Enum.TryParse<MatchType>(rule[1..rule.IndexOf('|', 1)], true, out var matchType) => new DefaultSetting(
				matchType, config[2], browser),
			
			// Unsupported format, ignore
			_ => null
		};
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

	[JsonIgnore]
	public string? SettingKey => ToString();

	[JsonIgnore]
	public string? SettingValue => Browser;

	[JsonIgnore]
	public bool Deleted
	{
		get => deleted;
		set
		{
			deleted = value;
			OnPropertyChanged();
		}
	}

	public string? Pattern
	{
		get => pattern;
		set
		{
			if (pattern == value)
			{
				return;
			}
			// Trigger deletion of old registry key
			OnPropertyChanging(nameof(SettingKey));
			pattern = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(IsValid));
			OnPropertyChanged(nameof(SettingKey));
		}
	}

	public string? Browser
	{
		get => browser;
		set
		{
			if (SetProperty(ref browser, value))
			{
				OnPropertyChanged(nameof(SettingValue));
			}
		}
	}

	[JsonIgnore]
	public bool IsValid => !string.IsNullOrWhiteSpace(pattern)
												 || pattern == string.Empty && Type == MatchType.Default;

	[JsonIgnore]
	public DelegateCommand Remove => new(() => { Deleted = true; });

	public int MatchLength(Uri url)
	{
		if (!IsValid)
		{
			return 0;
		}
		return Type switch
		{
			MatchType.Default => 1,
			MatchType.Hostname when pattern is not null => url.Host.EndsWith(pattern) ? pattern.Length : 0,
			MatchType.Prefix when pattern is not null => url.OriginalString.StartsWith(pattern) ? pattern.Length : 0,
			MatchType.Regex when pattern is not null => Regex.Match(url.OriginalString, pattern).Length,
			_ => 0
		};
	}

	public override string? ToString() => Type switch
	{
		MatchType.Hostname => pattern,
		MatchType.Default => string.Empty,
		_ => $"|{Type}|{pattern}"
	};

	public override int GetHashCode() => Type.GetHashCode() ^ pattern?.GetHashCode() ?? 0;

	private void OnPropertyChanging([CallerMemberName] string? propertyName = null)
	{
		PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
	}

	public event PropertyChangingEventHandler? PropertyChanging;
}