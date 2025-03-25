using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace BrowserPicker;

public interface IBrowserPickerConfiguration : IApplicationSettings, INotifyPropertyChanged, ILongRunningProcess
{
	/// <summary>
	/// When true, only urls matching some <see cref="IApplicationSettings.Defaults"/> record will give the user a choice.
	/// This makes BrowserPicker only seemingly apply for certain urls.
	/// </summary>
	public bool UseFallbackDefault { get; set; }

	/// <summary>
	/// The browser to use if <see cref="UseFallbackDefault"/> is true and no <see cref="IApplicationSettings.Defaults"/> match the url.
	/// </summary>
	public string? DefaultBrowser { get; set; }

	/// <summary>
	/// Add a new browser to the list
	/// </summary>
	void AddBrowser(BrowserModel browser);

	/// <summary>
	/// Scan the system for known browsers
	/// </summary>
	void FindBrowsers();

	/// <summary>
	/// Add a default setting rule to the configuration
	/// </summary>
	/// <param name="matchType">Type of match</param>
	/// <param name="pattern">The url fragment to match</param>
	/// <param name="browser">The browser to use</param>
	void AddDefault(MatchType matchType, string pattern, string browser);

	/// <summary>
	/// Exports all the configuration to a json file
	/// </summary>
	/// <param name="fileName">The full path of a json file</param>
	Task SaveAsync(string fileName);

	/// <summary>
	/// Imports all the configuration from a json file
	/// </summary>
	/// <param name="fileName">The full path of a json file</param>
	Task LoadAsync(string fileName);

	/// <summary>
	/// Logs from backup and restore
	/// </summary>
	public string BackupLog { get; }

	/// <summary>
	/// Sorter used to sort browsers
	/// </summary>
	IComparer<BrowserModel>? BrowserSorter { get; }
}