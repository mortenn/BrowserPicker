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
	/// The browser id of the fallback default browser when <see cref="UseFallbackDefault"/> is true and no <see cref="IApplicationSettings.Defaults"/> match the url.
	/// </summary>
	public string? DefaultBrowser { get; set; }

	/// <summary>
	/// Add a new browser to the list
	/// </summary>
	void AddBrowser(BrowserModel browser);

	/// <summary>
	/// Writes the current state of an existing browser to the registry.
	/// Use after editing a browser to ensure all properties (e.g. CommandArgs) are persisted.
	/// </summary>
	void PersistBrowser(BrowserModel browser);

	/// <summary>
	/// Scan the system for known browsers
	/// </summary>
	void FindBrowsers();

	/// <summary>
	/// Add a default setting rule to the configuration.
	/// </summary>
	/// <param name="matchType">Type of match.</param>
	/// <param name="pattern">The url fragment to match.</param>
	/// <param name="browser">The browser id or display name to use for this rule.</param>
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