using System.Collections.Generic;

namespace BrowserPicker;

public class BrowserSorter(IApplicationSettings configuration) : IComparer<BrowserModel>
{
	public int Compare(BrowserModel? x, BrowserModel? y)
	{
		if (x == null || y == null)
		{
			return x == null && y == null ? 0 : (x == null ? -1 : 1);
		}
		if (configuration.UseAlphabeticalOrdering)
		{
			return x.Name.CompareTo(y.Name);
		}
		if (configuration.UseManualOrdering)
		{
			return x.ManualOrder.CompareTo(y.ManualOrder);
		}
		return y.Usage.CompareTo(x.Usage);
	}
}