using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using BrowserPicker.Framework;
using BrowserPicker.View;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
#if DEBUG
using JetBrains.Annotations;
#endif

namespace BrowserPicker.ViewModel;

/// <summary>
/// Represents the main view model for the application. Manages application state, 
/// configuration, and browser selection behavior.
/// </summary>
public sealed class ApplicationViewModel : ModelBase
{
	private static ILogger<ApplicationViewModel> Logger =>
		ReferenceEquals(App.Services, null)
			? NullLogger<ApplicationViewModel>.Instance
			: App.Services.GetService<ILogger<ApplicationViewModel>>() ?? NullLogger<ApplicationViewModel>.Instance;
	
#if DEBUG
	/// <summary>
	/// Default constructor used for WPF designer support.
	/// Initializes the URL handler, configuration, and sets up default browser choices.
	/// </summary>
	[UsedImplicitly]
	public ApplicationViewModel()
	{
		if (ReferenceEquals(App.Settings, null))
		{
			var designSettings = ConfigurationViewModel.CreateDesignTimeSettings();
			Url = new UrlHandler(NullLogger<UrlHandler>.Instance, "https://github.com/mortenn/BrowserPicker", designSettings);
			force_choice = true;
			Choices = [];
			Configuration = new ConfigurationViewModel(designSettings, this);
			foreach (var choice in designSettings.BrowserList
				         .OrderBy(b => b, new BrowserSorter(designSettings))
				         .Select(b => new BrowserViewModel(b, this)))
			{
				Choices.Add(choice);
			}
			ApplyAutoCloseOnFocusLostSetting();
			return;
		}

		Url = new UrlHandler();
		force_choice = true;
		Choices = [];
		Configuration = new ConfigurationViewModel(App.Settings, this);
		foreach (var choice in App.Settings.BrowserList
			         .OrderBy(b => b, new BrowserSorter(App.Settings))
			         .Select(b => new BrowserViewModel(b, this)))
		{
			Choices.Add(choice);
		}
		ApplyAutoCloseOnFocusLostSetting();
	}

	/// <summary>
	/// Alternate constructor primarily meant for WPF designer support.
	/// Initializes URL handler, configuration, and an empty browser choices list.
	/// </summary>
	/// <param name="config">The configuration view model to initialize the application state.</param>
	internal ApplicationViewModel(ConfigurationViewModel config)
	{
		Url = new UrlHandler(NullLogger<UrlHandler>.Instance, "https://github.com/mortenn/BrowserPicker", config.Settings);
		force_choice = true;
		Configuration = config;
		Choices = [];
		ApplyAutoCloseOnFocusLostSetting();
	}
#endif

	/// <summary>
	/// Main constructor for initializing the view model with command-line arguments and application settings.
	/// </summary>
	/// <param name="arguments">Command-line arguments passed to the application.</param>
	/// <param name="settings">Configuration settings for the browser picker.</param>
	public ApplicationViewModel(IReadOnlyCollection<string> arguments, IBrowserPickerConfiguration settings)
	{
		var options = arguments.Where(arg => arg[0] == '/').ToList();
		force_choice = options.Contains("/choose");
		var url = arguments.Except(options).FirstOrDefault();
		// TODO Refactor to use IoC
		Url = new UrlHandler(App.Services.GetRequiredService<ILogger<UrlHandler>>(), url, settings);
		ConfigurationMode = url == null;
		Choices = [];
		Configuration = new ConfigurationViewModel(settings, this)
		{
			ParentViewModel = this
		};
		var sorter = settings.BrowserSorter ?? new BrowserSorter(settings);
		var choices = settings.BrowserList.OrderBy(m => m, sorter).Select(m => new BrowserViewModel(m, this)).ToList();
		foreach (var choice in choices)
		{
			Choices.Add(choice);
		}
		ApplyAutoCloseOnFocusLostSetting();
	}

	/// <summary>
	/// Gets the URL handler for the current target URL (resolution, favicon, display).
	/// </summary>
	public UrlHandler Url { get; }

	/// <summary>
	/// Initializes the application state. Handles first-time setup, configuration mode,
	/// and optional automatic browser launch based on URL and settings.
	/// </summary>
	/// <returns><see langword="true"/> when the main window should be shown; otherwise <see langword="false"/>.</returns>
	public bool Initialize()
	{
		if (Configuration.Settings.FirstTime)
		{
			Configuration.Welcome = true;
			ConfigurationMode = true;
			Configuration.Settings.FirstTime = false;
			return true;
		}

		if (
			Url.TargetURL == null
			|| Keyboard.Modifiers == ModifierKeys.Alt
			|| Configuration.Settings.AlwaysPrompt
			|| ConfigurationMode
			|| force_choice)
		{
			return true;
		}

		var (start, profile) = GetBrowserToLaunch(Url.UnderlyingTargetURL ?? Url.TargetURL);
		Logger.LogAutomationChoice(start?.Model.Name);

#if DEBUG
		if (Debugger.IsAttached && start != null)
		{
			Debug.WriteLine($"Skipping launch of browser {start.Model.Name} due to debugger being attached");
			return true;
		}
#endif
		if (start == null)
		{
			return true;
		}

		start.LaunchWithProfile(false, profile);
		return false;
	}

	/// <summary>
	/// Determines and retrieves the appropriate browser (and optional profile) to launch based on the provided URL.
	/// </summary>
	/// <param name="targetUrl">The URL to match against browser rules and settings.</param>
	/// <returns>A tuple of the browser view model and optional profile to launch, or (null, null) if none is chosen.</returns>
	internal (BrowserViewModel? Browser, BrowserProfile? Profile) GetBrowserToLaunch(string? targetUrl)
	{
		Logger.LogAutomationInputs(
			targetUrl,
			Configuration.Settings.AlwaysPrompt,
			Configuration.Settings.AlwaysUseDefaults,
			Configuration.Settings.AlwaysAskWithoutDefault,
			Configuration.Settings.UseFallbackDefault,
			Configuration.Settings.DefaultBrowser);
		if (Configuration.Settings.AlwaysPrompt)
		{
			Logger.LogAutomationAlwaysPrompt();
			return (null, null);
		}
		var (urlBrowserId, profileId) = GetBrowserToLaunchForUrl(targetUrl);
		var browser = ResolveBrowser(urlBrowserId, includeDisabled: false);
		var profile = ResolveProfile(browser, profileId);
		Logger.LogAutomationBrowserSelected(browser?.Model.Name, browser?.IsRunning);
		if (browser != null && (Configuration.Settings.AlwaysUseDefaults || browser.IsRunning))
		{
			Logger.LogAutomationUsingConfiguredBrowser(
				browser.Model.Name,
				Configuration.Settings.AlwaysUseDefaults,
				browser.IsRunning);
			return (browser, profile);
		}
		if (browser != null)
		{
			Logger.LogAutomationSkippingConfiguredBrowser(
				browser.Model.Name,
				Configuration.Settings.AlwaysUseDefaults,
				browser.IsRunning);
		}
		if (browser == null && Configuration.Settings.AlwaysAskWithoutDefault)
		{
			Logger.LogAutomationAlwaysPromptWithoutDefaults();
			return (null, null);
		}
		var active = Choices.Where(b => b is { IsRunning: true, Model.Disabled: false }).ToList();
		Logger.LogAutomationRunningCount(active.Count);
		Logger.LogAutomationRunningBrowsers(string.Join(", ", active.Select(b => $"{b.Model.Name}({b.Model.Id})")));
		if (active.Count == 1)
		{
			Logger.LogAutomationSingleRunningBrowser(active[0].Model.Name);
			return (active[0], null);
		}
		return (null, null);
	}

	/// <summary>
	/// Matches the given URL against configured rules to determine the preferred browser and profile for the URL.
	/// </summary>
	/// <param name="targetUrl">The URL to evaluate against browser rules.</param>
	/// <returns>A tuple of the browser id and optional profile id, or (null, null) if none is found.</returns>
	internal (string? BrowserId, string? ProfileId) GetBrowserToLaunchForUrl(string? targetUrl)
	{
		var (matchedKey, profileId) = GetMatchedBrowserKeyForUrl(targetUrl);
		if (string.IsNullOrWhiteSpace(matchedKey))
		{
			return (null, null);
		}

		var resolved = ResolveBrowser(matchedKey, includeDisabled: true);
		Logger.LogAutomationResolvedBrowser(
			matchedKey,
			resolved?.Model.Id,
			resolved?.Model.Name,
			resolved?.Model.Disabled,
			resolved?.Model.Removed);

		if (resolved is not { Model.Disabled: false, Model.Removed: false })
		{
			return (null, null);
		}

		return (resolved.Model.Id, profileId);
	}

	/// <summary>
	/// Matches the given URL against configured rules and returns the configured browser key and optional profile,
	/// even when that browser is currently disabled.
	/// </summary>
	internal (string? BrowserKey, string? ProfileId) GetMatchedBrowserKeyForUrl(string? targetUrl)
	{
		if (Configuration.Settings.Defaults.Count <= 0 || string.IsNullOrWhiteSpace(targetUrl))
		{
			Logger.LogAutomationNoDefaultsConfigured();
			return (null, null);
		}

		Uri url;
		try
		{
			url = new Uri(targetUrl);
		}
		catch (UriFormatException)
		{
			Logger.LogAutomationInvalidUrl(targetUrl);
			return (null, null);
		}
		var auto = Configuration.Settings.Defaults
			.Select(rule => new { rule, matchLength = rule.MatchLength(url) })
			.Where(o => o.matchLength > 0)
			.ToList();

		Logger.LogAutomationMatchesFound(auto.Count);
		foreach (var match in auto.OrderByDescending(o => o.matchLength))
		{
			Logger.LogAutomationMatchCandidate(
				match.rule.Type.ToString(),
				match.rule.Pattern,
				match.rule.Browser,
				match.matchLength);
		}

		string? matchedKey = null;
		string? matchedProfile = null;
		string? matchedSource = null;
		if (auto.Count > 0)
		{
			var best = auto.OrderByDescending(o => o.matchLength).First().rule;
			matchedKey = best.Browser;
			matchedProfile = best.Profile;
			matchedSource = "rule";
		}
		else if (Configuration.Settings.UseFallbackDefault && !string.IsNullOrWhiteSpace(Configuration.Settings.DefaultBrowser))
		{
			matchedKey = Configuration.Settings.DefaultBrowser;
			matchedSource = "fallback";
		}

		Logger.LogAutomationMatchedKey(matchedKey, matchedSource ?? "none");
		return (matchedKey, matchedProfile);
	}

	private BrowserViewModel? ResolveBrowser(string? browserKey, bool includeDisabled)
	{
		if (string.IsNullOrWhiteSpace(browserKey))
		{
			return null;
		}

		return Choices.FirstOrDefault(c =>
			(includeDisabled || !c.Model.Disabled)
			&& !c.Model.Removed
			&& (c.Model.Id == browserKey || c.Model.Name == browserKey));
	}

	private static BrowserProfile? ResolveProfile(BrowserViewModel? browser, string? profileId)
	{
		if (browser == null || string.IsNullOrWhiteSpace(profileId))
		{
			return null;
		}

		return browser.Model.Profiles.FirstOrDefault(p =>
			!p.Disabled
			&& string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Toggles the application's configuration mode state.
	/// </summary>
	public ICommand Configure => new DelegateCommand(() => ConfigurationMode = !ConfigurationMode);

	/// <summary>
	/// Opens configuration mode directly on the feedback tab.
	/// </summary>
	public ICommand Feedback => new DelegateCommand(OpenFeedback);

	/// <summary>
	/// Closes the application by triggering the shutdown event.
	/// </summary>
	public ICommand Exit => new DelegateCommand(() => OnShutdown?.Invoke(this, EventArgs.Empty));

	/// <summary>
	/// Copies the currently targeted URL to the system clipboard.
	/// </summary>
	public ICommand CopyUrl => new DelegateCommand(PerformCopyUrl);

	/// <summary>
	/// Opens the URL editor, allowing the user to modify the currently targeted URL.
	/// </summary>
	public ICommand Edit => new DelegateCommand(OpenURLEditor);
	
	/// <summary>
	/// Gets the view model responsible for managing application configuration settings.
	/// Provides access to user preferences and saved browser configurations.
	/// </summary>
	public ConfigurationViewModel Configuration { get; }

	/// <summary>
	/// Gets the list of browsers presented to the user.
	/// Allowing the user to select a browser based on specific criteria or preferences.
	/// </summary>
	public ObservableCollection<BrowserViewModel> Choices { get; }

	/// <summary>
	/// Gets or sets a value indicating whether the application is in configuration mode.
	/// Configuration mode displays settings and bypasses automatic browser selection during startup.
	/// </summary>
	public bool ConfigurationMode
	{
		get => configuration_mode;
		set
		{
			SetProperty(ref configuration_mode, value);
		}
	}

	/// <summary>
	/// Gets or sets a value indicating whether the targeted URL has been successfully copied to the clipboard.
	/// Used to indicate the url was copied in the View.
	/// </summary>
	public bool Copied
	{
		get => copied;
		set => SetProperty(ref copied, value);
	}

	/// <summary>
	/// Gets or sets a value indicating whether the Alt key is pressed, signalling the users intent to activate privacy mode.
	/// </summary>
	public bool AltPressed
	{
		get => alt_pressed;
		set => SetProperty(ref alt_pressed, value);
	}
	
	/// <summary>
	/// Toggles whether this picker window stays open when it loses focus.
	/// </summary>
	public DelegateCommand PinWindow => new(() => Pinned = !Pinned);

	/// <summary>
	/// Gets or sets a value indicating whether the main application window is pinned.
	/// When pinned, the application bypasses certain automatic shutdown conditions.
	/// </summary>
	public bool Pinned
	{
		get => pinned;
		private set => SetProperty(ref pinned, value);
	}
	
	/// <summary>
	/// Event triggered to initiate application shutdown. 
	/// It is wired to the <see cref="App.ExitApplication" /> method to terminate the application.
	/// </summary>
	public EventHandler? OnShutdown;

	/// <summary>
	/// Performs application shutdown when the main window becomes inactive, 
	/// unless specific conditions like configuration mode or pinning are met.
	/// </summary>
	public void OnDeactivated()
	{
		if (!ConfigurationMode && !Debugger.IsAttached && Configuration.AutoCloseOnFocusLost && !Pinned)
		{
			OnShutdown?.Invoke(this, EventArgs.Empty);
		}
	}

	internal void ApplyAutoCloseOnFocusLostSetting()
	{
		OnPropertyChanged(nameof(Pinned));
	}

	private void OpenFeedback()
	{
		Configuration.ShowFeedbackTab();
		ConfigurationMode = true;
	}

	/// <summary>
	/// Copies the underlying target URL to the clipboard in a thread-safe manner.
	/// </summary>
	private void PerformCopyUrl()
	{
		try
		{
			if (Url.UnderlyingTargetURL == null)
			{
				return;
			}
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

	/// <summary>
	/// Opens the editor for the current URL, preparing it for user modifications.
	/// </summary>
	private void OpenURLEditor()
	{
		var editor = new UrlEditor(Url.UnderlyingTargetURL)
		{
			Owner = Application.Current?.MainWindow
		};
		if (editor.ShowDialog() == true)
		{
			Url.UnderlyingTargetURL = editor.EditedUrl;
		}
	}

	/// <summary>
	/// Reorders the list of browser choices based on the user's settings.
	/// </summary>
	internal void RefreshChoices()
	{
		var newOrder = Choices.OrderBy(c => c.Model, Configuration.Settings.BrowserSorter).ToArray();
		Choices.Clear();
		foreach (var choice in newOrder)
		{
			Choices.Add(choice);
		}
	}

	private bool configuration_mode;
	private bool alt_pressed;
	private readonly bool force_choice;
	private bool pinned;
	private bool copied;
}
