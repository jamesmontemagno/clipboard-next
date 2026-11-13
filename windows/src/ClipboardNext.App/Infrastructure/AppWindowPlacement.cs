using ClipboardNext.Core;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace ClipboardNext.App;

internal static class AppWindowPlacement
{
    private const double DefaultDpi = 96.0;

    public static void CenterInCurrentWorkAreaAtDipSize(AppWindow appWindow, nint hwnd, int widthDip, int heightDip)
    {
        var scale = GetScaleForWindow(hwnd);
        var workArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Clamp(DipToPixels(widthDip, scale), 1, workArea.Width);
        var height = Math.Clamp(DipToPixels(heightDip, scale), 1, workArea.Height);
        var x = workArea.X + ((workArea.Width - width) / 2);
        var y = workArea.Y + ((workArea.Height - height) / 2);

        appWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    public static AppWindowTarget PrepareForTargetWorkArea(AppWindow appWindow, nint hwnd, RectInt32 workArea)
    {
        // Callers use this while the window is hidden. Moving first makes WinUI process the
        // per-monitor DPI transition before content is measured or sized.
        var transitionX = workArea.X + Math.Max(0, (workArea.Width - appWindow.Size.Width) / 2);
        var transitionY = workArea.Y + Math.Max(0, (workArea.Height - appWindow.Size.Height) / 2);
        appWindow.Move(new PointInt32(transitionX, transitionY));
        return new AppWindowTarget(workArea, GetScaleForWindow(hwnd));
    }

    public static int DipToPixels(double valueDip, double scale) =>
        Math.Max(1, (int)Math.Round(valueDip * (scale > 0 ? scale : 1.0)));

    public static RectInt32 ClampToWorkArea(RectInt32 workArea, int x, int y, int width, int height)
    {
        width = Math.Clamp(width, 1, Math.Max(1, workArea.Width));
        height = Math.Clamp(height, 1, Math.Max(1, workArea.Height));
        x = Math.Clamp(x, workArea.X, workArea.X + Math.Max(0, workArea.Width - width));
        y = Math.Clamp(y, workArea.Y, workArea.Y + Math.Max(0, workArea.Height - height));
        return new RectInt32(x, y, width, height);
    }

    public static double GetScaleForWindow(nint hwnd)
    {
        var dpi = GetDpiForWindow(hwnd);
        return dpi == 0 ? 1.0 : dpi / DefaultDpi;
    }

    public static PixelRect ToPixelRect(RectInt32 rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
}

internal readonly record struct AppWindowTarget(RectInt32 WorkArea, double Scale);
