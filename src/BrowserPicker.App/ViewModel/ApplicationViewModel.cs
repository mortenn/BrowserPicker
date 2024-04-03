using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using BrowserPicker.Framework;
#if DEBUG
using JetBrains.Annotations;
#endif

namespace BrowserPicker.ViewModel;

public sealed class ApplicationViewModel : ModelBase
{
#if DEBUG
	// Used by WPF designer
	[UsedImplicitly]
	public ApplicationViewModel()
	{
		Url = new UrlHandler();
		force_choice = true;
		Configuration = new ConfigurationViewModel(App.Settings);
		Choices = new ObservableCollection<BrowserViewModel>(
			WellKnownBrowsers.List.Select(b => new BrowserViewModel(new BrowserModel(b, null, null), this))
		);
	}
	
	internal ApplicationViewModel(ConfigurationViewModel config)
	{
		Url = new UrlHandler(config.Settings, "https://github.com/mortenn/BrowserPicker");
		force_choice = true;
		Configuration = config;
		Choices = new ObservableCollection<BrowserViewModel>(
			WellKnownBrowsers.List.Select(b => new BrowserViewModel(new BrowserModel(b, null, null), this))
		);
	}
#endif

	public ApplicationViewModel(IReadOnlyCollection<string> arguments, IBrowserPickerConfiguration settings)
	{
		var options = arguments.Where(arg => arg[0] == '/').ToList();
		force_choice = options.Contains("/choose");
		var url = arguments.Except(options).FirstOrDefault();
		if (url != null)
		{
			Url = new UrlHandler(settings, url);
		}
		ConfigurationMode = url == null;
		Configuration = new ConfigurationViewModel(settings)
		{
			ParentViewModel = this
		};
		Choices = new ObservableCollection<BrowserViewModel>(settings.BrowserList.Select(m => new BrowserViewModel(m, this)));
	}

	public UrlHandler Url { get; }

	public void Initialize()
	{
		if (Keyboard.Modifiers == ModifierKeys.Alt)
		{
			return;
		}

		if (Configuration.AlwaysPrompt || ConfigurationMode || force_choice)
		{
			return;
		}

		BrowserViewModel start = GetBrowserToLaunch(Url.UnderlyingTargetURL ?? Url.TargetURL);
#if DEBUG
		if (Debugger.IsAttached && start != null)
		{
			Debug.WriteLine($"Skipping launch of browser {start.Model.Name} due to debugger being attached");
			return;
		}
#endif
		start?.Select.Execute(null);
	}

	internal BrowserViewModel GetBrowserToLaunch(string targetUrl)
	{
		if (Configuration.AlwaysPrompt || ConfigurationMode || force_choice)
		{
			return null;
		}
		var urlBrowser = GetBrowserToLaunchForUrl(targetUrl);
		var browser = Choices.FirstOrDefault(c => c.Model.Name == urlBrowser);
		if (browser != null && (Configuration.AlwaysUseDefaults || browser.IsRunning))
		{
			return browser;
		}
		if (browser == null && Configuration.Settings.AlwaysAskWithoutDefault)
		{
			return null;
		}
		var active = Choices.Where(b => b.IsRunning && !b.Model.Disabled).ToList();
		return active.Count == 1 ? active[0] : null;
	}

	internal string GetBrowserToLaunchForUrl(string targetUrl)
	{
		if (Configuration.Settings.Defaults.Count <= 0 || string.IsNullOrWhiteSpace(targetUrl))
			return null;

		Uri url;
		try
		{
			url = new Uri(targetUrl);
		}
		catch (UriFormatException)
		{
			return null;
		}
		var auto = Configuration.Settings.Defaults
			.Select(rule => new { rule, matchLength = rule.MatchLength(url) })
			.Where(o => o.matchLength > 0)
			.ToList();

		return auto.Count <= 0
			? null
			: auto.OrderByDescending(o => o.matchLength).First().rule.Browser;
	}

	public ICommand Configure => new DelegateCommand(() => ConfigurationMode = !ConfigurationMode);

	public ICommand Exit => new DelegateCommand(() => OnShutdown?.Invoke(this, EventArgs.Empty));

	public ICommand CopyUrl => new DelegateCommand(PerformCopyUrl);

	public ICommand Edit => new DelegateCommand(OpenURLEditor);

	public ConfigurationViewModel Configuration { get; }

	public ObservableCollection<BrowserViewModel> Choices { get; }

	public bool ConfigurationMode
	{
		get => configuration_mode;
		set
		{
			SetProperty(ref configuration_mode, value);
		}
	}
	/*
	public string TargetURL
	{
		get => target_url;
		private set
		{
			if (Equals(target_url, value))
			{
				return;
			}
			target_url = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(IsShortenedURL));
		}
	}

	public string UnderlyingTargetURL
	{
		get => actual_url;
		set
		{
			if (Equals(actual_url, value))
			{
				return;
			}
			actual_url = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(IsShortenedURL));
		}
	}
	*/

	public string EditURL
	{
		get => edit_url;
		set
		{
			SetProperty(ref edit_url, value);
			Url.UnderlyingTargetURL = value;
		}
	}

	public bool Copied
	{
		get => copied;
		set => SetProperty(ref copied, value);
	}

	public bool AltPressed
	{
		get => alt_pressed;
		set => SetProperty(ref alt_pressed, value);
	}

	public DelegateCommand PinWindow => new(() => Pinned = true);

	public bool Pinned
	{
		get => pinned;
		private set => SetProperty(ref pinned, value);
	}

	public EventHandler OnShutdown;

	public void OnDeactivated()
	{
		if (!ConfigurationMode && !Debugger.IsAttached && !Pinned)
		{
			OnShutdown?.Invoke(this, EventArgs.Empty);
		}
	}

	private void PerformCopyUrl()
	{
		try
		{
			var thread = new Thread(() => Clipboard.SetText(Url.UnderlyingTargetURL));
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			Copied = true;
		}
		catch
		{
			// ignored
		}
	}

	private void OpenURLEditor()
	{
		EditURL = Url.UnderlyingTargetURL;
		OnPropertyChanged(nameof(EditURL));
	}

	private bool configuration_mode;
	private string edit_url;
	private bool alt_pressed;
	private readonly bool force_choice;
	private bool pinned;
	private bool copied;
}
