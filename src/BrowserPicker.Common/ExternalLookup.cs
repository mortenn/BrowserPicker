using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BrowserPicker.Common;

/// <summary>
/// An opt-in lookup that sends part of a URL to a third-party service during a manual connection check.
/// </summary>
public interface IExternalLookup
{
	/// <summary>
	/// Tells the user, before the check starts, which service receives which value.
	/// Returns null when this lookup has nothing to send for the URL.
	/// </summary>
	string? DescribeRequest(Uri url);

	/// <summary>
	/// Runs the lookup. Failures, rate limits and unsupported inputs are returned as a neutral result,
	/// never thrown, and never presented as a security finding.
	/// </summary>
	Task<ExternalLookupResult> LookupAsync(Uri url, CancellationToken cancellationToken);
}

/// <summary>
/// The outcome of an external lookup, shown as one section of the connection check.
/// </summary>
/// <param name="Title">Section title.</param>
/// <param name="Summary">One-line summary shown next to the title.</param>
/// <param name="Details">Label and value rows; rows with an empty value are hidden.</param>
public sealed record ExternalLookupResult(
	string Title,
	string Summary,
	IReadOnlyList<KeyValuePair<string, string?>> Details
);
