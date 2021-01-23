using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Win32;

namespace BrowserPicker.Configuration
{
	public class Config
	{
		public bool AlwaysPrompt
		{
			get => Reg.Get<bool>(nameof(AlwaysPrompt));
			set => Reg.Set(nameof(AlwaysPrompt), value);
		}

		public IEnumerable<Browser> BrowserList
		{
			get => GetBrowsers();
			set => SetBrowsers(value);
		}

		public IEnumerable<DefaultSetting> Defaults
		{
			get => GetDefaults();
			set => SetDefaults(value);
		}

		public bool DefaultsWhenRunning
		{
			get => Reg.Get<bool>(nameof(DefaultsWhenRunning));
			set => Reg.Set(nameof(DefaultsWhenRunning), value);
		}

		public int UrlLookupTimeoutMilliseconds
		{
			get => Reg.Get(nameof(UrlLookupTimeoutMilliseconds), 500);
			set => Reg.Set(nameof(UrlLookupTimeoutMilliseconds), value);
		}

		public static void UpdateCounter(Browser browser)
		{
			Reg
				.OpenSubKey(Path.Combine(nameof(BrowserList), browser.Name), true)
				?.SetValue(nameof(browser.Usage), browser.Usage + 1, RegistryValueKind.DWord);
		}

		public static void UpdateBrowserDisabled(Browser browser)
		{
			Reg
				.OpenSubKey(Path.Combine(nameof(BrowserList), browser.Name), true)
				?.SetValue(nameof(browser.Disabled), browser.Disabled ? 1 : 0, RegistryValueKind.DWord);
		}

		public static void RemoveBrowser(Browser browser)
		{
			Reg.DeleteSubKeyTree(Path.Combine(nameof(BrowserList), browser.Name), false);
		}

		public static void RemoveDefault(string fragment)
		{
			Reg.OpenSubKey(nameof(Defaults), true)?.DeleteValue(fragment);
		}

		public static void SetDefault(string fragment, string browser)
		{
			Reg.CreateSubKey(nameof(Defaults), true).SetValue(fragment, browser, RegistryValueKind.String);
		}

		[SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
		private static void SetDefaults([NotNull] IEnumerable<DefaultSetting> defaults)
		{
			var key = Reg.CreateSubKey(nameof(Defaults), true);
			var values = key.GetValueNames();
			foreach (var fragment in values.Except(defaults.Select(d => d.Fragment)))
				key.DeleteValue(fragment);
			foreach (var setting in defaults)
				key.SetValue(setting.Fragment, setting.Browser, RegistryValueKind.String);
		}

		private static IEnumerable<DefaultSetting> GetDefaults()
		{
			var key = Reg.CreateSubKey(nameof(Defaults), true);
			var values = key.GetValueNames();
			return values.Select(
				fragment => new DefaultSetting(fragment, (string)key.GetValue(fragment))
			).ToList();
		}

		[SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
		private static void SetBrowsers([NotNull] IEnumerable<Browser> browsers)
		{
			var list = Reg.CreateSubKey(nameof(BrowserList), true);
			foreach (var remove in list.GetSubKeyNames().Except(browsers.Select(b => b.Name)))
				list.DeleteSubKeyTree(remove);
			foreach (var browser in browsers)
			{
				var key = list.CreateSubKey(browser.Name, true);
				key.Set(nameof(browser.Name), browser.Name);
				key.Set(nameof(browser.Command), browser.Command);
				key.Set(nameof(browser.CommandArgs), browser.CommandArgs);
				key.Set(nameof(browser.IconPath), browser.IconPath);
				key.Set(nameof(browser.Usage), browser.Usage);
			}
			list.Close();
		}

		private static IEnumerable<Browser> GetBrowsers()
		{

			var list = Reg.OpenSubKey("BrowserList", true);
			if (list == null)
				return new List<Browser>();

			var browsers = list.GetSubKeyNames()
				.Select(browser => GetBrowser(list, browser))
				.Where(browser => browser != null)
				.OrderByDescending(b => b.Usage)
				.ToList();

			if (browsers.Any(browser => browser.Name.Equals("Microsoft Edge")))
			{
				var edge = browsers.FirstOrDefault(browser => browser.Name.Equals("Edge"));
				if (edge != null)
				{
					browsers.Remove(edge);
					list.DeleteSubKeyTree(edge.Name);
				}
			}

			list.Close();
			return browsers;
		}

		private static Browser GetBrowser(RegistryKey list, string name)
		{
			var config = list.OpenSubKey(name, false);
			if (config == null) return null;
			var browser = new Browser
			{
				Name = name,
				Command = config.Get<string>(nameof(Browser.Command)),
				CommandArgs = config.Get<string>(nameof(Browser.CommandArgs)),
				IconPath = config.Get<string>(nameof(Browser.IconPath)),
				Usage = config.Get<int>(nameof(Browser.Usage)),
				Disabled = config.Get<bool>(nameof(Browser.Disabled))
			};
			config.Close();
			return browser.Command == null ? null : browser;
		}

		private static readonly RegistryKey Reg = Registry.CurrentUser.CreateSubKey("Software\\BrowserPicker", true);
	}
}
