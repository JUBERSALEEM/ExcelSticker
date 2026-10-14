using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ExcelSticker.Theming;

/// <summary>Keep studio actions in the current monitor's work area, including high-DPI desktops.</summary>
public static class WindowPlacement
{
    public static void FitOnFirstShow(Window window)
    {
        window.Loaded += Fit;
        void Fit(object sender,RoutedEventArgs args)
        {
            window.Loaded -= Fit;
            var info=new MonitorInfo {Size=Marshal.SizeOf<MonitorInfo>()};
            var monitor=MonitorFromWindow(new WindowInteropHelper(window).Handle,2);
            if(!GetMonitorInfo(monitor,ref info))return;
            var dpi=VisualTreeHelper.GetDpi(window);
            var availableWidth=Math.Max(320,(info.Work.Right-info.Work.Left)/dpi.DpiScaleX-24);
            var availableHeight=Math.Max(320,(info.Work.Bottom-info.Work.Top)/dpi.DpiScaleY-24);
            var oldWidth=window.ActualWidth;var oldHeight=window.ActualHeight;
            window.MinWidth=Math.Min(window.MinWidth,availableWidth);window.MinHeight=Math.Min(window.MinHeight,availableHeight);
            var width=Math.Min(oldWidth,availableWidth);var height=Math.Min(oldHeight,availableHeight);
            window.Width=width;window.Height=height;
            window.Left+=(oldWidth-width)/2;window.Top+=(oldHeight-height)/2;
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo {public int Size;public Rect Monitor;public Rect Work;public uint Flags;}
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
}
