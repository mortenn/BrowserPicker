using System.Threading;
using System.Threading.Tasks;

namespace BrowserPicker;

/// <summary>
/// Represents a process that runs asynchronously and can be cancelled.
/// </summary>
public interface ILongRunningProcess
{
	/// <summary>
	/// Starts the long-running process.
	/// </summary>
	/// <param name="cancellationToken">Token to cancel the operation.</param>
	/// <returns>A task that completes when the process finishes or is cancelled.</returns>
	Task Start(CancellationToken cancellationToken);
}
