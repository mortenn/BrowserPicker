using BrowserPicker.Framework;
using System.ComponentModel;
using JetBrains.Annotations;
using System.Threading;
using System.Linq;
using System.Collections.ObjectModel;
using System.Windows.Input;
using BrowserPicker.View;
using System.Windows;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BrowserPicker.ViewModel;

public sealed class ConfigurationViewModel : ModelBase
{
#if DEBUG
	// Used by WPF designer
	[UsedImplicitly]
	public ConfigurationViewModel()
	{
		Settings = new DesignTimeSettings
		{
			Defaults = [
				new DefaultSetting(MatchType.Hostname, "github.com", Firefox.Instance.Name),
				new DefaultSetting(MatchType.Prefix, "https://gitlab.com", Edge.Instance.Name),
				new DefaultSetting(MatchType.Regex, @"runsafe\.no\/[0-9a-f]+$", InternetExplorer.Instance.Name),
				new DefaultSetting(MatchType.Hostname, "gitlab.com", OperaStable.Instance.Name),
				new DefaultSetting(MatchType.Hostname, "microsoft.com", MicrosoftEdge.Instance.Name),
				new DefaultSetting(MatchType.Default, "", Firefox.Instance.Name)
			],
			BrowserList = [],
			DefaultBrowser = Firefox.Instance.Name
		};
		foreach (var setting in Settings.Defaults.Where(d => d.Type != MatchType.Default))
		{
			Defaults.Add(setting);
		}
		ParentViewModel = new ApplicationViewModel(this)
		{
			ConfigurationMode = true
		};
		Settings.BrowserList.AddRange(ParentViewModel.Choices.Select(m => m.Model));
		AvailableBrowsers = [.. ParentViewModel.Choices];
		test_defaults_url = ParentViewModel.Url?.UnderlyingTargetURL ?? ParentViewModel.Url?.TargetURL;
	}

	private sealed class DesignTimeSettings : IBrowserPickerConfiguration
	{
		public bool AlwaysPrompt { get; set; } = true;
		public bool AlwaysUseDefaults { get; set; } = true;
		public bool AlwaysAskWithoutDefault { get; set; }
		public int UrlLookupTimeoutMilliseconds { get; set; } = 2000;
		public bool UseAutomaticOrdering { get; set; } = true;
		public bool DisableTransparency { get; set; } = true;
		public bool DisableNetworkAccess { get; set; } = false;

		public List<BrowserModel> BrowserList { get; init; }

		public List<DefaultSetting> Defaults { get; init; }

		public bool UseFallbackDefault { get; set; } = true;
		public string DefaultBrowser { get; set; }

		public event PropertyChangedEventHandler PropertyChanged;

		public void AddBrowser(BrowserModel browser)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BrowserList)));
		}

		public DefaultSetting AddDefault(MatchType matchType, string pattern, string browser)
		{
			return new DefaultSetting(matchType, pattern, browser);
		}

		public void FindBrowsers()
		{
		}

		public Task Start(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}
#endif

	public ConfigurationViewModel(IBrowserPickerConfiguration settings)
	{
		Defaults.CollectionChanged += Defaults_CollectionChanged;
		Settings = settings;
		foreach (var setting in Settings.Defaults.Where(d => d.Type != MatchType.Default))
		{
			Defaults.Add(setting);
		}
		settings.PropertyChanged += Configuration_PropertyChanged;
	}

	private void Defaults_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
	{
		if (e.NewItems?.Count > 0)
		{
			foreach (var item in e.NewItems.OfType<DefaultSetting>())
			{
				item.PropertyChanged += Item_PropertyChanged;
			}
		}

		if (!(e.OldItems?.Count > 0))
		{
			return;
		}

		foreach (var item in e.OldItems.OfType<DefaultSetting>())
		{
			item.PropertyChanged -= Item_PropertyChanged;
		}
	}

	private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(DefaultSetting.Deleted) && sender is DefaultSetting { Deleted: true } item)
		{
			Defaults.Remove(item);
		}
	}

	public IBrowserPickerConfiguration Settings { get; init; }

	public ApplicationViewModel ParentViewModel { get; init; }

	/// <summary>
	/// Only read by init code on app startup
	/// </summary>
	public bool AlwaysPrompt => Settings.AlwaysPrompt;

	/// <summary>
	/// Only read by init code on app startup
	/// </summary>
	public bool AlwaysUseDefaults => Settings.AlwaysUseDefaults;

	public bool AutoAddDefault
	{
		get => auto_add_default;
		set => SetProperty(ref auto_add_default, value);
	}

	public List<BrowserViewModel> AvailableBrowsers { get; init; }

	public ObservableCollection<DefaultSetting> Defaults { get; } = [];

	public MatchType NewDefaultMatchType
	{
		get => new_match_type;
		set
		{
			// Ignore user if they select the special Default value and pretend they wanted Hostname
			if (value == MatchType.Default)
			{
				if (!SetProperty(ref new_match_type, MatchType.Hostname))
				{
					// Always fire in this case
					OnPropertyChanged();
				}
				return;
			}
			SetProperty(ref new_match_type, value);
		}
	}

	public string NewDefaultPattern { get => new_fragment; set => SetProperty(ref new_fragment, value); }

	public string NewDefaultBrowser { get => new_fragment_browser; set => SetProperty(ref new_fragment_browser, value); }

	public ICommand AddDefault => add_default ??= new DelegateCommand(AddDefaultSetting);

	public ICommand RefreshBrowsers => refresh_browsers ??= new DelegateCommand(FindBrowsers);

	public ICommand AddBrowser => add_browser ??= new DelegateCommand(AddBrowserManually);

	private void AddBrowserManually()
	{
		var editor = new BrowserEditor();
		editor.Show();
		editor.Closing += Editor_Closing;
	}

	private void Editor_Closing(object sender, CancelEventArgs e)
	{
		((Window)sender).Closing -= Editor_Closing;
		if (sender is not Window { DataContext: BrowserViewModel browser })
		{
			return;
		}
		if (string.IsNullOrEmpty(browser.Model.Name) || string.IsNullOrEmpty(browser.Model.Command))
		{
			return;
		}
		ParentViewModel.Choices.Add(browser);
		Settings.AddBrowser(browser.Model);
	}

	private void AddDefaultSetting()
	{
		if (string.IsNullOrWhiteSpace(NewDefaultPattern) || string.IsNullOrWhiteSpace(NewDefaultBrowser))
		{
			return;
		}
		Settings.AddDefault(NewDefaultMatchType, NewDefaultPattern, NewDefaultBrowser);
		NewDefaultPattern = string.Empty;
		OnPropertyChanged(nameof(TestDefaultsResult));
	}

	private void Configuration_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(Settings.Defaults):
				UpdateDefaults();
				break;

			case nameof(Settings.BrowserList):
				UpdateSettings();
				break;
		}
	}

	private void UpdateSettings()
	{
		var added = Settings.BrowserList.Where(b => ParentViewModel.Choices.All(c => c.Model.Name != b.Name)).ToList();
		if (added.Count > 0)
		{
			foreach (var vm in added.Select(m => new BrowserViewModel(m, ParentViewModel)))
			{
				ParentViewModel.Choices.Add(vm);
			}
		}
		var removed = ParentViewModel.Choices.Where(c => Settings.BrowserList.All(b => b.Name != c.Model.Name)).ToList();
		if (removed.Count <= 0)
		{
			return;
		}
		foreach (var m in removed)
		{
			ParentViewModel.Choices.Remove(m);
		}
	}

	private void UpdateDefaults()
	{
		var added = Settings.Defaults.Except(Defaults).Where(i => i.Type != MatchType.Default).ToArray();
		if (added.Length > 0)
		{
			foreach (var setting in added)
			{
				Defaults.Add(setting);
			}
		}
		var removed = Defaults.Except(Settings.Defaults).ToArray();
		if (removed.Length <= 0)
		{
			return;
		}

		foreach (var setting in removed)
		{
			Defaults.Remove(setting);
		}
	}

	internal CancellationTokenSource GetUrlLookupTimeout()
	{
		return new CancellationTokenSource(Settings.UrlLookupTimeoutMilliseconds);
	}

	internal void FindBrowsers()
	{
		Settings.FindBrowsers();
		OnPropertyChanged(nameof(TestDefaultsResult));
	}

	private MatchType new_match_type = MatchType.Hostname;
	private string new_fragment;
	private string new_fragment_browser;
	private bool auto_add_default;
	private DelegateCommand add_default;
	private DelegateCommand refresh_browsers;
	private DelegateCommand add_browser;

	private string test_defaults_url;

	public string TestDefaultsURL
	{
		get => test_defaults_url;
		set
		{
			SetProperty(ref test_defaults_url, value);
			OnPropertyChanged(nameof(TestDefaultsResult));
			OnPropertyChanged(nameof(TestActualResult));
		}
	}

	public string TestDefaultsResult
	{
		get
		{
			return ParentViewModel.GetBrowserToLaunchForUrl(test_defaults_url) ?? "User choice";
		}
	}

	public string TestActualResult
	{
		get
		{
			return ParentViewModel.GetBrowserToLaunch(test_defaults_url)?.Model.Name ?? "User choice";
		}
	}
}
