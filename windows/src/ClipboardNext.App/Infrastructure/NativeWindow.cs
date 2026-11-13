using System.Runtime.InteropServices;

namespace ClipboardNext.App;

/// <summary>Win32 window tweaks that WinUI does not expose.</summary>
internal static class NativeWindow
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const long WS_EX_NOACTIVATE = 0x08000000;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    /// <summary>
    /// Makes a window that never takes keyboard focus when shown, clicked, or dragged, and that
    /// stays out of Alt+Tab and the taskbar. The app the presenter is typing into keeps focus.
    /// </summary>
    public static void MakeNonActivatingToolWindow(nint hwnd)
    {
        var style = GetWindowLongPtrW(hwnd, GWL_EXSTYLE);
        SetWindowLongPtrW(hwnd, GWL_EXSTYLE, (nint)(style | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW));
    }

    /// <summary>Asks Windows 11 to round the corners of a borderless window.</summary>
    public static void RoundCorners(nint hwnd)
    {
        var preference = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
    }

    public static void BringToForeground(nint hwnd) => SetForegroundWindow(hwnd);

    // The app builds for x64 and ARM64 only, so the pointer-sized variants always exist.
    [DllImport("user32.dll")]
    private static extern nint GetWindowLongPtrW(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern nint SetWindowLongPtrW(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hwnd);
}
