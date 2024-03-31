﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using BrowserPicker.Framework;
using BrowserPicker.View;
using JetBrains.Annotations;

namespace BrowserPicker.ViewModel
{
	public class ApplicationViewModel : INotifyPropertyChanged
	{
		// Used by WPF designer
		[UsedImplicitly]
		public ApplicationViewModel()
		{
			Url = new UrlHandler(App.Settings, "https://github.com/mortenn/BrowserPicker");
			force_choice = true;
			Configuration = new (App.Settings);
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

		public ApplicationViewModel(List<string> arguments, IBrowserPickerConfiguration settings)
		{
			var options = arguments.Where(arg => arg[0] == '/').ToList();
			force_choice = options.Contains("/choose");
			var url = arguments.Except(options).FirstOrDefault();
			if (url != null)
			{
				Url = new UrlHandler(settings, url);
			}
			ConfigurationMode = url == null;
			Configuration = new(settings)
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
			if (Debugger.IsAttached)
			{
				Debug.WriteLine($"Skipping launch of browser {start.Model.Name} due to debugger being attached");
				return;
			}
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
			if (browser != null)
			{
				return browser;
			}
			var active = Choices.Where(b => b.IsRunning && !b.Model.Disabled).ToList();
			return active.Count == 1 ? active[0] : null;
		}

		internal string GetBrowserToLaunchForUrl(string targetUrl)
		{
			if (Configuration.Settings.Defaults.Count <= 0 || string.IsNullOrWhiteSpace(targetUrl))
				return null;

			Uri url = null;
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
			
			if (auto.Count <= 0)
				return null;

			return auto.OrderByDescending(o => o.matchLength).First().rule.Browser;
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
				configuration_mode = value;
				OnPropertyChanged();
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
				edit_url = value;
				Url.UnderlyingTargetURL = value;
			}
		}

		public bool Copied { get; set; }

		public bool AltPressed
		{
			get => alt_pressed;
			set
			{
				if (alt_pressed == value) return;
				alt_pressed = value;
				OnPropertyChanged();
			}
		}

		public DelegateCommand PinWindow => new(() => Pinned = true);

		public bool Pinned
		{
			get => pinned;
			private set
			{
				if (value == pinned) return;
				pinned = value;
				OnPropertyChanged();
			}
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
				OnPropertyChanged(nameof(Copied));
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

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private bool configuration_mode;
		private string edit_url;
		private bool alt_pressed;
		private readonly bool force_choice;
		private bool pinned;
	}
}
