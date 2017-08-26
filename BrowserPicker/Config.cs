using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using BrowserPicker.Annotations;
using Microsoft.Win32;

namespace BrowserPicker
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
				key.Set(nameof(browser.IconPath), browser.IconPath);
				key.Set(nameof(browser.Usage), browser.Usage);
			}
			list.Close();
		}

		private static IEnumerable<Browser> GetBrowsers()
		{
			var browsers = new List<Browser>();

			var list = Reg.OpenSubKey("BrowserList", true);
			if (list == null)
				return browsers;

			foreach (var browser in list.GetSubKeyNames())
			{
				var config = list.OpenSubKey(browser, false);
				browsers.Add(
					new Browser
					{
						Name = browser,
						Command = config.Get<string>(nameof(Browser.Command)),
						IconPath = config.Get<string>(nameof(Browser.IconPath)),
						Usage = config.Get<int>(nameof(Browser.Usage)),
						Disabled = config.Get<bool>(nameof(Browser.Disabled))
					}
				);
			}
			list.Close();
			return browsers.OrderByDescending(b => b.Usage).ToList();
		}

		private static readonly RegistryKey Reg = Registry.CurrentUser.CreateSubKey("Software\\BrowserPicker", true);
	}
}
