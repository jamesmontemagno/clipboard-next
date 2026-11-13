using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace ClipboardNext.App;

/// <summary>
/// Drag-anywhere behavior for a borderless floating window. Window movement is anchored to the
/// absolute cursor position rather than a pointer-relative offset, to avoid feedback jitter as
/// the window moves under the pointer.
/// </summary>
/// <remarks>
/// Wire the methods to <see cref="UIElement.PointerPressed"/>, <see cref="UIElement.PointerMoved"/>,
/// <see cref="UIElement.PointerReleased"/>, <see cref="UIElement.PointerCanceled"/>, and
/// <see cref="UIElement.PointerCaptureLost"/> on the drag surface.
/// </remarks>
internal sealed class FloatingWindowDragger
{
    private readonly AppWindow _appWindow;
    private bool _dragging;
    private POINT _cursorStart;
    private PointInt32 _windowStart;

    public FloatingWindowDragger(AppWindow appWindow)
    {
        _appWindow = appWindow;
    }

    /// <summary>Raised when a drag that moved the window has ended.</summary>
    public event EventHandler? Moved;

    public void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        GetCursorPos(out _cursorStart);
        _windowStart = _appWindow.Position;
        _dragging = element.CapturePointer(e.Pointer);
    }

    public void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        GetCursorPos(out var current);
        var dx = current.X - _cursorStart.X;
        var dy = current.Y - _cursorStart.Y;
        if (dx == 0 && dy == 0)
        {
            return;
        }

        _appWindow.Move(new PointInt32(_windowStart.X + dx, _windowStart.Y + dy));
    }

    public void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        EndDrag();
        element.ReleasePointerCapture(e.Pointer);
    }

    /// <summary>Ends the drag when WinUI cancels the interaction or pointer capture is lost.</summary>
    public void OnPointerCaptureEnded(object sender, PointerRoutedEventArgs e) => EndDrag();

    private void EndDrag()
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        var position = _appWindow.Position;
        if (position.X != _windowStart.X || position.Y != _windowStart.Y)
        {
            Moved?.Invoke(this, EventArgs.Empty);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);
}
