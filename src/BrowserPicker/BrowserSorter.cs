using System;
using System.Collections.Generic;

namespace BrowserPicker;

public class BrowserSorter(IApplicationSettings configuration) : IComparer<BrowserModel>
{
	public int Compare(BrowserModel? x, BrowserModel? y)
	{
		if (x == null || y == null)
		{
			return x == null && y == null ? 0 : x == null ? -1 : 1;
		}

		return configuration.UseAlphabeticalOrdering switch
		{
			true => string.Compare(x.Name, y.Name, StringComparison.Ordinal),
			false when configuration.UseManualOrdering => x.ManualOrder.CompareTo(y.ManualOrder),
			_ => y.Usage.CompareTo(x.Usage)
		};
	}
}