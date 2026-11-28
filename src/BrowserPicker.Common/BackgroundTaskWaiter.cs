using System.Threading;
using System.Threading.Tasks;

namespace BrowserPicker.Common;

/// <summary>
/// Waits for startup background work with an upper bound, so the UI is never held past the configured timeout.
/// </summary>
public static class BackgroundTaskWaiter
{
	/// <summary>
	/// Waits for <paramref name="backgroundTasks"/> to complete, but no longer than until <paramref name="timeout"/>
	/// or <paramref name="applicationExit"/> is cancelled.
	/// </summary>
	/// <param name="backgroundTasks">The in-flight background work; it is left running if the wait is cancelled.</param>
	/// <param name="timeout">Token that cancels when the configured URL lookup timeout expires.</param>
	/// <param name="applicationExit">Token that cancels when the application is shutting down.</param>
	/// <exception cref="TaskCanceledException">Either token was cancelled before the background work completed.</exception>
	public static async Task WaitWithTimeout(
		Task backgroundTasks,
		CancellationToken timeout,
		CancellationToken applicationExit
	)
	{
		using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout, applicationExit);
		await backgroundTasks.WaitAsync(linked.Token);
	}
}
