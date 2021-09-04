using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace BrowserPicker.View
{
	/// <summary>
	/// Interaction logic for Exception.xaml
	/// </summary>
	public partial class ExceptionReport
	{
		public ExceptionReport()
		{
			InitializeComponent();
			cancellationTokenSource = new CancellationTokenSource();
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);
			cancellationTokenSource.Cancel();
		}

		public void Wait()
		{
			try
			{
				Task.Delay(-1, cancellationTokenSource.Token);
			}
			catch(TaskCanceledException)
			{
				// ignore
			}
		}

		private CancellationTokenSource cancellationTokenSource;
	}
}
