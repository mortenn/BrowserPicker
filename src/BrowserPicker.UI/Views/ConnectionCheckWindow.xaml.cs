using System;
using BrowserPicker.UI.ViewModels;

namespace BrowserPicker.UI.Views;

/// <summary>
/// Interaction logic for ConnectionCheckWindow.xaml
/// </summary>
public partial class ConnectionCheckWindow
{
#if DEBUG
	public ConnectionCheckWindow()
	{
		InitializeComponent();
	}
#endif

	public ConnectionCheckWindow(ConnectionCheckViewModel viewModel)
	{
		DataContext = viewModel;
		viewModel.CloseRequested += ViewModel_CloseRequested;
		InitializeComponent();
		Loaded += ConnectionCheckWindow_Loaded;
	}

	protected override void OnClosed(EventArgs e)
	{
		if (DataContext is ConnectionCheckViewModel viewModel)
		{
			viewModel.CloseRequested -= ViewModel_CloseRequested;
		}
		Loaded -= ConnectionCheckWindow_Loaded;

		base.OnClosed(e);
	}

	private void ConnectionCheckWindow_Loaded(object sender, EventArgs e)
	{
		if (DataContext is ConnectionCheckViewModel viewModel)
		{
			viewModel.StartIfRequested();
		}
	}

	private void ViewModel_CloseRequested(object? sender, EventArgs e)
	{
		DialogResult = true;
	}
}
