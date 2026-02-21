using BrowserPicker.Framework;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BrowserPicker;

/// <summary>
/// Represents a per-URL default browser rule: when a URL matches the pattern, the associated browser is preferred.
/// </summary>
[JsonConverter(typeof(DefaultSettingJsonConverter))]
public sealed class DefaultSetting(MatchType initialType, string? initialPattern, string? initialBrowser) : ModelBase, INotifyPropertyChanging
{
	private readonly Guid id = Guid.NewGuid();
	private MatchType type = initialType;
	private string? pattern = initialPattern;
	private string? browser = initialBrowser;
	private bool deleted;

	/// <summary>
	/// Parses a registry or encoded rule string into a <see cref="DefaultSetting"/>.
	/// </summary>
	/// <param name="rule">The rule string (e.g. hostname, or "|MatchType|pattern"). Use null for a new rule with no pattern.</param>
	/// <param name="browser">The browser id or display name to assign to the decoded rule.</param>
	/// <returns>A new <see cref="DefaultSetting"/>, or null if the format is unsupported.</returns>
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
			1 when rule == string.Empty
				=> new DefaultSetting(MatchType.Default, string.Empty, browser),
			
			// Original hostname match type
			<= 1
				=> new DefaultSetting(MatchType.Hostname, rule, browser),
			
			// New configuration format using MatchType
			3 when Enum.TryParse<MatchType>(rule[1..rule.IndexOf('|', 1)], true, out var matchType)
				=> new DefaultSetting(matchType, config[2], browser),
			
			// Unsupported format, ignore
			_ => null
		};
	}

	/// <summary>
	/// Gets or sets how the URL is matched: hostname suffix, prefix, regex, contains, or default (fallback).
	/// </summary>
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

	/// <summary>
	/// Registry key for this rule; derived from <see cref="Type"/> and <see cref="Pattern"/>.
	/// </summary>
	[JsonIgnore]
	public string? SettingKey => ToString();

	/// <summary>
	/// Registry value for this rule; the browser id or name stored for this default.
	/// </summary>
	[JsonIgnore]
	public string? SettingValue => Browser;

	/// <summary>
	/// When true, this rule is marked for removal (e.g. deleted in the UI).
	/// </summary>
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

	/// <summary>
	/// The URL fragment or pattern to match, depending on <see cref="Type"/>. Empty for <see cref="MatchType.Default"/>.
	/// </summary>
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

	/// <summary>
	/// Gets or sets the browser id (or display name for backward compatibility) for this default rule.
	/// Used to identify which browser to launch when the URL matches.
	/// </summary>
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

	/// <summary>
	/// True when the rule has a valid pattern, or is a default (fallback) rule with empty pattern.
	/// </summary>
	[JsonIgnore]
	public bool IsValid => !string.IsNullOrWhiteSpace(pattern)
		|| pattern == string.Empty && Type == MatchType.Default;

	/// <summary>
	/// Command that marks this rule as deleted.
	/// </summary>
	[JsonIgnore]
	public DelegateCommand Remove => new(() => { Deleted = true; });

	/// <summary>
	/// Returns the length of the match when this rule is applied to the given URL; 0 if no match.
	/// Longer matches take precedence when choosing a default browser.
	/// </summary>
	/// <param name="url">The URL to match.</param>
	/// <returns>Match length, or 0 if the rule does not match.</returns>
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
			MatchType.Contains when pattern is not null => url.OriginalString.Contains(pattern) ? pattern.Length : 0,
			_ => 0
		};
	}

	/// <inheritdoc />
	public override string? ToString() => Type switch
	{
		MatchType.Hostname => pattern,
		MatchType.Default => string.Empty,
		_ => $"|{Type}|{pattern}"
	};

	/// <inheritdoc />
	public override int GetHashCode() => Type.GetHashCode() ^ id.GetHashCode();

	private void OnPropertyChanging([CallerMemberName] string? propertyName = null)
	{
		PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
	}

	/// <summary>
	/// Raised before a property value changes.
	/// </summary>
	public event PropertyChangingEventHandler? PropertyChanging;
}