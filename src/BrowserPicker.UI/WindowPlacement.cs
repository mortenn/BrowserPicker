using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BrowserPicker.UI;

/// <summary>
/// Places windows on the right monitor in multi-monitor setups.
/// WPF's <see cref="SystemParameters.PrimaryScreenWidth"/> only describes the primary monitor, and
/// <see cref="WindowStartupLocation.CenterScreen"/> uses the monitor under the mouse, so mixing them
/// shows the window on one monitor and then moves it to another.
/// </summary>
internal static class WindowPlacement
{
	/// <summary>Windows that have been rendered at least once; later re-centering follows the window's own monitor.</summary>
	private static readonly ConditionalWeakTable<Window, object> rendered_windows = new();

	/// <summary>
	/// Positions <paramref name="window"/> on its target monitor before it is first shown, so it never
	/// appears on one monitor and then jumps to another. Call from the constructor, after InitializeComponent.
	/// </summary>
	public static void Attach(Window window)
	{
		window.WindowStartupLocation = WindowStartupLocation.Manual;
		window.SourceInitialized += (_, _) => Center(window, EstimateSize(window));
		window.ContentRendered += (_, _) => rendered_windows.AddOrUpdate(window, rendered_windows);
	}

	/// <summary>
	/// Centers <paramref name="window"/> within the work area of its target monitor. The target is the
	/// primary monitor when <see cref="Common.IApplicationSettings.CenterOnPrimaryMonitor"/> is set. Otherwise
	/// it is the monitor the window is on once shown, or the monitor under the mouse cursor before that.
	/// </summary>
	public static void Center(Window window, Size size)
	{
		var hwnd = new WindowInteropHelper(window).Handle;
		var monitor = IntPtr.Zero;
		if (App.Settings?.CenterOnPrimaryMonitor == true)
			monitor = MonitorFromPoint(default, MONITOR_DEFAULTTOPRIMARY);
		else if (hwnd != IntPtr.Zero && rendered_windows.TryGetValue(window, out _))
			monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
		if (monitor == IntPtr.Zero && GetCursorPos(out var cursor))
			monitor = MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST);

		var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
		if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
		{
			CenterOnPrimary(window, size);
			return;
		}

		// Work area is in physical pixels; convert to the DIP coordinate space WPF uses for Left/Top.
		var source = hwnd != IntPtr.Zero ? HwndSource.FromHwnd(hwnd) : null;
		var fromDevice = source?.CompositionTarget?.TransformFromDevice;
		var scaleX = fromDevice?.M11 ?? 96.0 / GetSystemDpi();
		var scaleY = fromDevice?.M22 ?? scaleX;

		var work = info.rcWork;
		var left = work.Left * scaleX;
		var top = work.Top * scaleY;
		var width = (work.Right - work.Left) * scaleX;
		var height = (work.Bottom - work.Top) * scaleY;

		window.Left = left + Math.Max(0, (width - size.Width) / 2);
		window.Top = top + Math.Max(0, (height - size.Height) / 2);
	}

	private static Size EstimateSize(Window window)
	{
		if (!double.IsNaN(window.Width) && !double.IsNaN(window.Height))
			return new Size(window.Width, window.Height);
		window.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
		var desired = window.DesiredSize;
		return new Size(
			double.IsNaN(window.Width) ? desired.Width : window.Width,
			double.IsNaN(window.Height) ? desired.Height : window.Height
		);
	}

	private static void CenterOnPrimary(Window window, Size size)
	{
		var area = SystemParameters.WorkArea;
		window.Left = area.Left + Math.Max(0, (area.Width - size.Width) / 2);
		window.Top = area.Top + Math.Max(0, (area.Height - size.Height) / 2);
	}

	private static double GetSystemDpi()
	{
		var dpi = GetDpiForSystem();
		return dpi > 0 ? dpi : 96.0;
	}

	private const uint MONITOR_DEFAULTTOPRIMARY = 1;
	private const uint MONITOR_DEFAULTTONEAREST = 2;

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT
	{
		public int X;
		public int Y;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MONITORINFO
	{
		public int cbSize;
		public RECT rcMonitor;
		public RECT rcWork;
		public uint dwFlags;
	}

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetCursorPos(out POINT point);

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromPoint(POINT point, uint flags);

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

	[DllImport("user32.dll")]
	private static extern uint GetDpiForSystem();
}
