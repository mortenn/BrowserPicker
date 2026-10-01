using AwesomeAssertions;

namespace BrowserPicker.Common.Tests;

/// <summary>
/// Regression tests for the startup wait in App.StartupBackgroundTasks: the picker window must show
/// no later than the configured URL lookup timeout, even when browser discovery or URL resolution
/// is still running. The old implementation passed the timeout token to Task.Run, which only
/// prevents the delegate from starting — once the inner Wait was running, the timeout was ignored.
/// </summary>
public class BackgroundTaskWaiterTests
{
	[Fact]
	public async Task Wait_is_cancelled_when_timeout_expires_while_background_tasks_still_run()
	{
		var backgroundTasks = new TaskCompletionSource();
		using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		var wait = BackgroundTaskWaiter.WaitWithTimeout(
			backgroundTasks.Task,
			timeout.Token,
			CancellationToken.None
		);

		var completed = await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(5)));

		completed.Should().Be(wait, "the wait must end no later than the timeout, even while tasks are running");
		var act = async () => await wait;
		await act.Should().ThrowAsync<TaskCanceledException>();
		backgroundTasks.SetResult();
	}

	[Fact]
	public async Task Wait_completes_when_background_tasks_finish_before_timeout()
	{
		var backgroundTasks = new TaskCompletionSource();
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

		var wait = BackgroundTaskWaiter.WaitWithTimeout(
			backgroundTasks.Task,
			timeout.Token,
			CancellationToken.None
		);
		backgroundTasks.SetResult();

		var act = async () => await wait;
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task Wait_is_cancelled_when_application_exits_while_background_tasks_still_run()
	{
		var backgroundTasks = new TaskCompletionSource();
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		using var applicationExit = new CancellationTokenSource();

		var wait = BackgroundTaskWaiter.WaitWithTimeout(backgroundTasks.Task, timeout.Token, applicationExit.Token);
		await applicationExit.CancelAsync();

		var completed = await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(5)));

		completed.Should().Be(wait, "application shutdown must not leave the wait hanging");
		var act = async () => await wait;
		await act.Should().ThrowAsync<TaskCanceledException>();
		backgroundTasks.SetResult();
	}
}
