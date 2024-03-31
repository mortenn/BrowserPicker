using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace BrowserPicker.View
{
	/// <summary>
	/// Interaction logic for Configuration.xaml
	/// </summary>
	public partial class Configuration
	{
		public Configuration()
		{
			InitializeComponent();
		}

		private async void Fragment_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
		{
			HoldoffTimer?.Cancel();
			CancellationTokenSource instance = new();
			HoldoffTimer = instance;
			try
			{
				await Task.Delay(HoldoffTime, instance.Token);
				if (instance.IsCancellationRequested)
					return;
				((TextBox)sender).GetBindingExpression(TextBox.TextProperty).UpdateSource();
			}
			catch (TaskCanceledException)
			{
				// ignored
			}
		}

		private static readonly TimeSpan HoldoffTime = TimeSpan.FromMilliseconds(200);
		private CancellationTokenSource HoldoffTimer = null;
	}
}
