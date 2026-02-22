using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using BrowserPicker.Framework;
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
	private static readonly ILogger<ApplicationViewModel> Logger = App.Services.GetRequiredService<ILogger<ApplicationViewModel>>();
	
#if DEBUG
	/// <summary>
	/// Default constructor used for WPF designer support.
	/// Initializes the URL handler, configuration, and sets up default browser choices.
	/// </summary>
	[UsedImplicitly]
	public ApplicationViewModel()
	{
		Url = new UrlHandler();
		force_choice = true;
		Configuration = new ConfigurationViewModel(App.Settings, this);
		Choices = new ObservableCollection<BrowserViewModel>(
			WellKnownBrowsers.List.Select(b => new BrowserViewModel(new BrowserModel(b, null, string.Empty), this))
		);
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
		Configuration = new ConfigurationViewModel(settings, this)
		{
			ParentViewModel = this
		};
		var sorter = settings.BrowserSorter ?? new BrowserSorter(settings);
		var choices = settings.BrowserList.OrderBy(m => m, sorter).Select(m => new BrowserViewModel(m, this)).ToList();
		Choices = new ObservableCollection<BrowserViewModel>(choices);
	}

	/// <summary>
	/// Gets the URL handler for the current target URL (resolution, favicon, display).
	/// </summary>
	public UrlHandler Url { get; }

	/// <summary>
	/// Initializes the application state. Handles first-time setup, configuration mode, 
	/// and optional automatic browser launch based on URL and settings.
	/// </summary>
	public void Initialize()
	{
		if (Configuration.Settings.FirstTime)
		{
			Configuration.Welcome = true;
			ConfigurationMode = true;
			Configuration.Settings.FirstTime = false;
			return;
		}

		if (
			Url.TargetURL == null
			|| Keyboard.Modifiers == ModifierKeys.Alt
			|| Configuration.Settings.AlwaysPrompt
			|| ConfigurationMode
			|| force_choice)
		{
			return;
		}

		BrowserViewModel? start = GetBrowserToLaunch(Url.UnderlyingTargetURL ?? Url.TargetURL);
		Logger.LogAutomationChoice(start?.Model.Name);

#if DEBUG
		if (Debugger.IsAttached && start != null)
		{
			Debug.WriteLine($"Skipping launch of browser {start.Model.Name} due to debugger being attached");
			return;
		}
#endif
		start?.Select.Execute(null);
	}

	/// <summary>
	/// Determines and retrieves the appropriate browser to launch based on the provided URL.
	/// </summary>
	/// <param name="targetUrl">The URL to match against browser rules and settings.</param>
	/// <returns>The browser view model to launch, or null if none is chosen.</returns>
	internal BrowserViewModel? GetBrowserToLaunch(string? targetUrl)
	{
		if (Configuration.Settings.AlwaysPrompt)
		{
			Logger.LogAutomationAlwaysPrompt();
			return null;
		}
		var urlBrowserId = GetBrowserToLaunchForUrl(targetUrl);
		var browser = urlBrowserId != null
			? Choices.FirstOrDefault(c => c.Model.Id == urlBrowserId)
			: null;
		Logger.LogAutomationBrowserSelected(browser?.Model.Name, browser?.IsRunning);
		if (browser != null && (Configuration.Settings.AlwaysUseDefaults || browser.IsRunning))
		{
			return browser;
		}
		if (browser == null && Configuration.Settings.AlwaysAskWithoutDefault)
		{
			Logger.LogAutomationAlwaysPromptWithoutDefaults();
			return null;
		}
		var active = Choices.Where(b => b is { IsRunning: true, Model.Disabled: false }).ToList();
		Logger.LogAutomationRunningCount(active.Count);
		return active.Count == 1 ? active[0] : null;
	}

	/// <summary>
	/// Matches the given URL against configured rules to determine the preferred browser for the URL.
	/// </summary>
	/// <param name="targetUrl">The URL to evaluate against browser rules.</param>
	/// <returns>The Id of the preferred browser for the URL, or null if none is found.</returns>
	internal string? GetBrowserToLaunchForUrl(string? targetUrl)
	{
		if (Configuration.Settings.Defaults.Count <= 0 || string.IsNullOrWhiteSpace(targetUrl))
		{
			Logger.LogAutomationNoDefaultsConfigured();
			return null;
		}

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

		Logger.LogAutomationMatchesFound(auto.Count);

		string? matchedKey = null;
		if (auto.Count > 0)
		{
			matchedKey = auto.OrderByDescending(o => o.matchLength).First().rule.Browser;
		}
		else if (Configuration.Settings.UseFallbackDefault && !string.IsNullOrWhiteSpace(Configuration.Settings.DefaultBrowser))
		{
			matchedKey = Configuration.Settings.DefaultBrowser;
		}

		return string.IsNullOrWhiteSpace(matchedKey) ? null : matchedKey;
	}

	/// <summary>
	/// Toggles the application's configuration mode state.
	/// </summary>
	public ICommand Configure => new DelegateCommand(() => ConfigurationMode = !ConfigurationMode);

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
	/// Closes the URL editor, saving any changes made to the targeted URL.
	/// </summary>
	public ICommand EndEdit => new DelegateCommand(CloseURLEditor);
	
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
	/// Gets or sets the URL being edited by the user, backing the URL editor functionality.
	/// Changes take effect in the underlying URL handler.
	/// </summary>
	public string? EditURL
	{
		get => edit_url;
		set
		{
			SetProperty(ref edit_url, value);
			Url.UnderlyingTargetURL = value!;
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
	/// Pins the window, keeping it around while the user does something else.
	/// </summary>
	public DelegateCommand PinWindow => new(() => Pinned = true);

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
		if (!ConfigurationMode && !Debugger.IsAttached && !Pinned)
		{
			OnShutdown?.Invoke(this, EventArgs.Empty);
		}
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
		EditURL = Url.UnderlyingTargetURL;
		OnPropertyChanged(nameof(EditURL));
	}

	/// <summary>
	/// Closes the URL editor and clears the edit state.
	/// </summary>
	private void CloseURLEditor()
	{
		if (edit_url == null)
		{
			return;
		}
		edit_url = null;
		OnPropertyChanged(nameof(EditURL));
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
	private string? edit_url;
	private bool alt_pressed;
	private readonly bool force_choice;
	private bool pinned;
	private bool copied;
}
