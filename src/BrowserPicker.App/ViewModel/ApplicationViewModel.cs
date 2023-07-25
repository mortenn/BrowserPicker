using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
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
			Configuration = App.Settings;
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
			Configuration = settings;
			Configuration.PropertyChanged += Configuration_PropertyChanged;
			Choices = new ObservableCollection<BrowserViewModel>(Configuration.BrowserList.Select(m => new BrowserViewModel(m, this)));
		}

		public UrlHandler Url { get; }

		private void Configuration_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(Configuration.BrowserList))
			{
				var added = Configuration.BrowserList.Where(b => Choices.All(c => c.Model.Name != b.Name)).ToList();
				if (added.Count > 0)
				{
					foreach(var vm in added.Select(m => new BrowserViewModel(m, this)))
					{
						Choices.Add(vm);
					}
				}
				var removed = Choices.Where(c => Configuration.BrowserList.All(b => b.Name != c.Model.Name)).ToList();
				if (removed.Count > 0)
				{
					foreach(var m in removed)
					{
						Choices.Remove(m);
					}
				}
			}
		}

		public void Initialize()
		{
			if (Configuration.AlwaysPrompt || ConfigurationMode || force_choice)
			{
				return;
			}
			if (Url != null)
			{
				CheckDefaultBrowser();
			}
			var active = Choices.Where(b => b.IsRunning && !b.Model.Disabled).ToList();
			if (active.Count == 1)
			{
				active[0].Select.Execute(null);
			}
		}

		private void CheckDefaultBrowser()
		{
			if (string.IsNullOrWhiteSpace(Url.TargetURL))
			{
				return;
			}
			var defaults = Configuration.Defaults.ToList();
			if (defaults.Count <= 0)
				return;

			var url = new Uri(Url.UnderlyingTargetURL ?? Url.TargetURL);
			var auto = defaults
				.Select(rule => new { rule, matchLength = rule.MatchLength(url) })
				.Where(o => o.matchLength > 0)
				.ToList();
			if (auto.Count <= 0 || Debugger.IsAttached)
				return;

			var browser = auto.OrderByDescending(o => o.matchLength).First().rule.Browser;
			var start = Choices.FirstOrDefault(c => c.Model.Name == browser);
			if (start == null || Configuration.DefaultsWhenRunning && !start.IsRunning)
				return;

			start.Select.Execute(null);
		}

		public ICommand RefreshBrowsers => new DelegateCommand(Configuration.FindBrowsers);

		public ICommand Configure => new DelegateCommand(() => ConfigurationMode = !ConfigurationMode);

		public ICommand Exit => new DelegateCommand(() => OnShutdown?.Invoke(this, EventArgs.Empty));

		public ICommand CopyUrl => new DelegateCommand(PerformCopyUrl);

		public ICommand Edit => new DelegateCommand(OpenURLEditor);

		public DelegateCommand AddBrowser => new DelegateCommand(AddBrowserManually);

		private void AddBrowserManually()
		{
			var editor = new BrowserEditor();
			editor.Show();
			editor.Closing += Editor_Closing;
		}

		private void Editor_Closing(object sender, CancelEventArgs e)
		{
			((Window)sender).Closing -= Editor_Closing;
			if (!(((Window)sender).DataContext is BrowserViewModel browser))
			{
				return;
			}
			if (string.IsNullOrEmpty(browser.Model.Name) || string.IsNullOrEmpty(browser.Model.Command))
			{
				return;
			}
			Choices.Add(browser);
			Configuration.AddBrowser(browser.Model);
		}

		public IBrowserPickerConfiguration Configuration { get; }

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

		public DelegateCommand PinWindow => new DelegateCommand(() => Pinned = true);

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
