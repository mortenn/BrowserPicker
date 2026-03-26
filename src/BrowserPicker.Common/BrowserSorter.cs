using System;
using System.Collections.Generic;

namespace BrowserPicker.Common;

/// <summary>
/// Compares browsers according to the current settings: alphabetical, manual order, or by usage.
/// </summary>
public class BrowserSorter(IApplicationSettings configuration) : IComparer<BrowserModel>
{
	/// <inheritdoc />
	public int Compare(BrowserModel? x, BrowserModel? y)
	{
		if (x == null || y == null)
		{
			return x == null && y == null ? 0
				: x == null ? -1
				: 1;
		}

		return configuration.SortBy switch
		{
			SerializableSettings.SortOrder.Alphabetical => string.Compare(x.Name, y.Name, StringComparison.Ordinal),
			SerializableSettings.SortOrder.Manual => x.ManualOrder.CompareTo(y.ManualOrder),
			_ => y.Usage.CompareTo(x.Usage),
		};
	}
}
