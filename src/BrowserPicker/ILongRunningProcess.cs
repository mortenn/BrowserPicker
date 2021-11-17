using System.Threading;
using System.Threading.Tasks;

namespace BrowserPicker;

public interface ILongRunningProcess
{
	Task Start(CancellationToken cancellationToken);
}
