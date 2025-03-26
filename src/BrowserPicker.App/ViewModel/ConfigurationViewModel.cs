using BrowserPicker.Framework;
using System.ComponentModel;
using System.Threading;
using System.Linq;
using System.Collections.ObjectModel;
using System.Windows.Input;
using BrowserPicker.View;
using System.Windows;
using System.Collections.Generic;
using Microsoft.Win32;
using System;
using System.Diagnostics;

#if DEBUG
using System.Threading.Tasks;
using JetBrains.Annotations;
#endif

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
			BrowserList = [.. WellKnownBrowsers.List.Select(b => new BrowserModel(b, null, string.Empty))],
			DefaultBrowser = Firefox.Instance.Name
		};
		Welcome = true;
		foreach (var setting in Settings.Defaults.Where(d => d.Type != MatchType.Default))
		{
			Defaults.Add(setting);
		}

		ParentViewModel = new ApplicationViewModel(this)
		{
			ConfigurationMode = true
		};
		var choices = Settings.BrowserList.OrderBy(v => v, new BrowserSorter(Settings)).Select(m => new BrowserViewModel(m, ParentViewModel));
		foreach (var choice in choices)
		{
			ParentViewModel.Choices.Add(choice);
		}
		test_defaults_url = ParentViewModel.Url.UnderlyingTargetURL ?? ParentViewModel.Url.TargetURL;
	}

	private sealed class DesignTimeSettings : IBrowserPickerConfiguration
	{
		public bool FirstTime { get; set; } = false;
		public bool AlwaysPrompt { get; set; } = true;
		public bool AlwaysUseDefaults { get; set; } = true;
		public bool AlwaysAskWithoutDefault { get; set; }
		public int UrlLookupTimeoutMilliseconds { get; set; } = 2000;
		public bool UseAutomaticOrdering { get; set; } = false;
		public bool UseManualOrdering { get; set; } = false;
		public bool UseAlphabeticalOrdering { get; set; } = true;
		public bool DisableTransparency { get; set; } = true;
		public bool DisableNetworkAccess { get; set; } = false;

		public string[] UrlShorteners { get; set; } = [..UrlHandler.DefaultUrlShorteners, "example.com"];

		public List<BrowserModel> BrowserList { get; init; } = [];

		public List<DefaultSetting> Defaults { get; init; } = [];
		public List<KeyBinding> KeyBindings { get; } = [];

		public bool UseFallbackDefault { get; set; } = true;
		public string? DefaultBrowser { get; set; }

		public event PropertyChangedEventHandler? PropertyChanged;

		public void AddBrowser(BrowserModel browser)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BrowserList)));
		}

		public string BackupLog => "Backup log comes here\nWith multiple lines of text\nmaybe\nsometimes\n...\n...\n...\n...\n...\n...\n...\n...\n...\n...\n...\n...\n...\n...\n...\n...\n...\n...\n...\n...\n...\n...\n...\n...\n...";

		public IComparer<BrowserModel>? BrowserSorter => null;

		public void AddDefault(MatchType matchType, string pattern, string browser)
		{
		}

		public void FindBrowsers()
		{
		}

		public Task LoadAsync(string fileName)
		{
			throw new UnreachableException();
		}

		public Task SaveAsync(string fileName)
		{
			throw new UnreachableException();
		}

		public Task Start(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}
#endif

	public ConfigurationViewModel(IBrowserPickerConfiguration settings, ApplicationViewModel parentViewModel)
	{
		ParentViewModel = parentViewModel;
		Defaults.CollectionChanged += Defaults_CollectionChanged;
		Settings = settings;
		foreach (var setting in Settings.Defaults.Where(d => d.Type != MatchType.Default))
		{
			Defaults.Add(setting);
		}
		settings.PropertyChanged += Configuration_PropertyChanged;
	}

	private void Defaults_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
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

	private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(DefaultSetting.Deleted) && sender is DefaultSetting { Deleted: true } item)
		{
			Defaults.Remove(item);
		}
	}

	public IBrowserPickerConfiguration Settings { get; }

	public ApplicationViewModel ParentViewModel { get; init; }

	public static string[] DefaultUrlShorteners => UrlHandler.DefaultUrlShorteners;

	public string[] AdditionalUrlShorteners => Settings.UrlShorteners.Except(DefaultUrlShorteners).ToArray();

	public bool Welcome { get; internal set; }

	public bool AutoAddDefault
	{
		get => auto_add_default;
		set => SetProperty(ref auto_add_default, value);
	}

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

	public ICommand Backup => backup ??= new DelegateCommand(PerformBackup);

	public ICommand Restore => restore ??= new DelegateCommand(PerformRestore);

	public ICommand AddShortener => add_shortener ??= new DelegateCommand<string>(AddUrlShortener, CanAddShortener);

	public ICommand RemoveShortener => remove_shortener ??= new DelegateCommand<string>(RemoveUrlShortener, CanRemoveShortener);

	private void PerformBackup()
	{
		var browser = new SaveFileDialog
		{
			FileName = "BrowserPicker.json",
			DefaultExt = ".json",
			Filter = "JSON Files (*.json)|*.json|All Files|*.*",
			CheckPathExists = true,
			DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
		};
		var result = browser.ShowDialog();
		if (result != true)
			return;
		Settings.SaveAsync(browser.FileName);
	}

	private void PerformRestore()
	{
		var browser = new OpenFileDialog
		{
			FileName = "BrowserPicker.json",
			DefaultExt = ".json",
			Filter = "JSON Files (*.json)|*.json|All Files|*.*",
			CheckPathExists = true,
			DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
		};
		var result = browser.ShowDialog();
		if (result != true)
			return;
		Settings.LoadAsync(browser.FileName);
	}

	private void AddBrowserManually()
	{
		var editor = new BrowserEditor(new BrowserViewModel(new BrowserModel(), ParentViewModel));
		editor.Show();
		editor.Closing += Editor_Closing;
	}

	public string NewUrlShortener { get; set; } = string.Empty;

	private bool CanAddShortener(string? domain) => !(string.IsNullOrWhiteSpace(domain) || Settings.UrlShorteners.Contains(domain));

	private void AddUrlShortener(string? domain)
	{
		if (!CanAddShortener(domain))
		{
			return;
		}
		Settings.UrlShorteners = [..Settings.UrlShorteners, domain!];
		NewUrlShortener = string.Empty;
		OnPropertyChanged(nameof(NewUrlShortener));
		OnPropertyChanged(nameof(DefaultUrlShorteners));
		OnPropertyChanged(nameof(AdditionalUrlShorteners));
	}

	private bool CanRemoveShortener(string? domain) => !string.IsNullOrWhiteSpace(domain) && Settings.UrlShorteners.Contains(domain) && !UrlHandler.DefaultUrlShorteners.Contains(domain);
	
	private void RemoveUrlShortener(string? domain)
	{
		if (!CanRemoveShortener(domain))
		{
			return;
		}

		Settings.UrlShorteners = Settings.UrlShorteners.Except([domain!]).ToArray();
		OnPropertyChanged(nameof(DefaultUrlShorteners));
		OnPropertyChanged(nameof(AdditionalUrlShorteners));
	}

	private void Editor_Closing(object? sender, CancelEventArgs e)
	{
		if (sender is Window window)
		{
			window.Closing -= Editor_Closing;
		}
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

	internal void UrlOpened(string? hostName, string browser)
	{
		if (!AutoAddDefault || hostName == null)
		{
			return;
		}

		try
		{
			AddNewDefault(MatchType.Hostname, hostName, browser);
		}
		catch
		{
			// ignored
		}
	}

	private bool AddNewDefault(MatchType matchType, string pattern, string browser)
	{
		if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(browser))
		{
			return false;
		}
		Settings.AddDefault(matchType, pattern, browser);
		OnPropertyChanged(nameof(TestDefaultsResult));
		return true;
	}

	private void AddDefaultSetting()
	{
		if (AddNewDefault(NewDefaultMatchType, NewDefaultPattern, NewDefaultBrowser))
		{
			NewDefaultPattern = string.Empty;
		}
	}

	private void Configuration_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(Settings.Defaults):
				UpdateDefaults();
				break;

			case nameof(Settings.BrowserList):
				UpdateSettings();
				break;

			case nameof(Settings.UseAutomaticOrdering) when Settings.UseAutomaticOrdering:
				CaptureBrowserOrder();
				break;
		}
	}

	private void CaptureBrowserOrder()
	{
		var browsers = Settings.BrowserList.Where(b => !b.Removed).ToArray();
		foreach (var browser in browsers)
		{
			browser.ManualOrder = Settings.BrowserList.IndexOf(browser);
		}
	}

	private void UpdateSettings()
	{
		BrowserViewModel[] added = [..
			from browser in Settings.BrowserList
			where ParentViewModel.Choices.All(c => c.Model.Name != browser.Name)
			select new BrowserViewModel(browser, ParentViewModel)
		];
		foreach (var vm in added)
		{
			ParentViewModel.Choices.Add(vm);
		}

		BrowserViewModel[] removed = [..
			from choice in ParentViewModel.Choices
			where Settings.BrowserList.All(b => b.Name != choice.Model.Name)
			select choice
		];
		foreach (var m in removed)
		{
			ParentViewModel.Choices.Remove(m);
		}
	}

	private void UpdateDefaults()
	{
		DefaultSetting[] added = [..
			from current in Settings.Defaults.Except(Defaults)
			where current.Type != MatchType.Default && !current.Deleted
			select current
		];
		foreach (var setting in added)
		{
			Defaults.Add(setting);
		}

		DefaultSetting[] removed = [.. Defaults.Except(Settings.Defaults)];
		foreach (var setting in removed)
		{
			Defaults.Remove(setting);
		}
	}

	internal CancellationTokenSource GetUrlLookupTimeout()
	{
		return new CancellationTokenSource(Settings.UrlLookupTimeoutMilliseconds);
	}

	private void FindBrowsers()
	{
		Settings.FindBrowsers();
		OnPropertyChanged(nameof(TestDefaultsResult));
	}

	private MatchType new_match_type = MatchType.Hostname;
	private string new_fragment = string.Empty;
	private string new_fragment_browser = string.Empty;
	private bool auto_add_default;
	private DelegateCommand? add_default;
	private DelegateCommand? refresh_browsers;
	private DelegateCommand? add_browser;
	private DelegateCommand? backup;
	private DelegateCommand? restore;
	private DelegateCommand<string>? add_shortener;
	private DelegateCommand<string>? remove_shortener;

	private string? test_defaults_url;

	public string? TestDefaultsURL
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
