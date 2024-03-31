using BrowserPicker.Framework;
using System.ComponentModel;
using JetBrains.Annotations;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Linq;
using System.Collections.ObjectModel;
using System.Windows.Input;
using BrowserPicker.View;
using System.Windows;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BrowserPicker.ViewModel
{
	public sealed class ConfigurationViewModel : INotifyPropertyChanged
	{
#if DEBUG
		// Used by WPF designer
		[UsedImplicitly]
		public ConfigurationViewModel()
		{
			Settings = new DesignTimeSettings
			{
				Defaults = [
					new(MatchType.Hostname, "github.com", Firefox.Instance.Name),
					new(MatchType.Prefix, "https://gitlab.com", Edge.Instance.Name),
					new(MatchType.Regex, "runsafe\\.no\\/[0-9a-f]+$", InternetExplorer.Instance.Name),
					new(MatchType.Hostname, "gitlab.com", OperaStable.Instance.Name),
					new(MatchType.Hostname, "microsoft.com", MicrosoftEdge.Instance.Name),
					new(MatchType.Default, "", Firefox.Instance.Name),
				],
				BrowserList = [],
				DefaultBrowser = Firefox.Instance.Name
			};
			foreach (var setting in Settings.Defaults.Where(d => d.Type != MatchType.Default))
			{
				Defaults.Add(setting);
			}
			ParentViewModel = new(this)
			{
				ConfigurationMode = true
			};
			Settings.BrowserList.AddRange(ParentViewModel.Choices.Select(m => m.Model));
			AvailableBrowsers = ParentViewModel.Choices.ToList();
			testDefaultsURL = ParentViewModel.Url?.UnderlyingTargetURL ?? ParentViewModel.Url?.TargetURL;
		}

		private sealed class DesignTimeSettings : IBrowserPickerConfiguration
		{
			public bool AlwaysPrompt { get; set; } = true;
			public bool DefaultsWhenRunning { get; set; } = true;
			public int UrlLookupTimeoutMilliseconds { get; set; } = 2000;
			public bool UseAutomaticOrdering { get; set; } = true;
			public bool DisableTransparency { get; set; } = true;
			public bool DisableNetworkAccess { get; set; } = false;

			public List<BrowserModel> BrowserList { get; init; }

			public List<DefaultSetting> Defaults { get; init; }

			public bool AlwaysUseDefault { get; set; } = true;
			public string DefaultBrowser { get; set; }

			public event PropertyChangedEventHandler PropertyChanged;

			public void AddBrowser(BrowserModel browser)
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BrowserList)));
			}

			public DefaultSetting AddDefault(MatchType matchType, string pattern, string browser)
			{
				return new(matchType, pattern, browser);
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
			Settings = settings;
			foreach(var setting in Settings.Defaults.Where(d => d.Type != MatchType.Default))
			{
				Defaults.Add(setting);
			}
			settings.PropertyChanged += Configuration_PropertyChanged;
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
		public bool DefaultsWhenRunning => Settings.DefaultsWhenRunning;

		public List<BrowserViewModel> AvailableBrowsers { get; init; }

		public ObservableCollection<DefaultSetting> Defaults { get; } = [];

		public MatchType NewDefaultMatchType
		{
			get => newMatchType;
			set
			{
				// Ignore user if they select the special Default value and pretend they wanted Hostname
				if (value == MatchType.Default)
				{
					if (!SetProperty(ref newMatchType, MatchType.Hostname))
					{
						// Always fire in this case
						OnPropertyChanged();
					}
					return;
				}
				SetProperty(ref newMatchType, value);
			} 
		}

		public string NewDefaultPattern { get => newFragment; set => SetProperty(ref newFragment, value); }

		public string NewDefaultBrowser { get => newFragmentBrowser; set => SetProperty(ref newFragmentBrowser, value); }

		public ICommand AddDefault => addDefault ??= new DelegateCommand(AddDefaultSetting);
		
		public ICommand RefreshBrowsers => refreshBrowsers ??= new DelegateCommand(FindBrowsers);

		public ICommand AddBrowser => addBrowser ??= new DelegateCommand(AddBrowserManually);

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
			if (e.PropertyName == nameof(Settings.Defaults))
			{
				var added = Settings.Defaults.Except(Defaults).ToArray();
				if (added.Length > 0)
				{
					foreach (var setting in added)
					{
						Defaults.Add(setting);
					}
				}
				var removed = Defaults.Except(Settings.Defaults).ToArray();
				if (removed.Length > 0)
				{
					foreach (var setting in removed)
					{
						Defaults.Remove(setting);
					}
				}
			}
			if (e.PropertyName == nameof(Settings.BrowserList))
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
				if (removed.Count > 0)
				{
					foreach (var m in removed)
					{
						ParentViewModel.Choices.Remove(m);
					}
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

		private bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null)
		{
			if (!Equals(field, newValue))
			{
				field = newValue;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
				return true;
			}

			return false;
		}

		private MatchType newMatchType = MatchType.Hostname;
		private string newFragment;
		private string newFragmentBrowser;
		private DelegateCommand addDefault;
		private DelegateCommand refreshBrowsers;
		private DelegateCommand addBrowser;

		private string testDefaultsURL;

		public string TestDefaultsURL
		{
			get => testDefaultsURL;
			set
			{
				SetProperty(ref testDefaultsURL, value);
				OnPropertyChanged(nameof(TestDefaultsResult));
				OnPropertyChanged(nameof(TestActualResult));
			}
		}

		public string TestDefaultsResult
		{
			get
			{
				return ParentViewModel.GetBrowserToLaunchForUrl(testDefaultsURL) ?? "User choice";
			}
		}

		public string TestActualResult
		{
			get
			{
				return ParentViewModel.GetBrowserToLaunch(testDefaultsURL)?.Model.Name ?? "User choice";
			}
		}
	}
}
