using ClipboardNext.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;

namespace ClipboardNext.App;

/// <summary>Applies the remembered appearance to a window and follows live system changes.</summary>
internal sealed class WindowThemeController
{
    private readonly Window _window;
    private readonly FrameworkElement _root;
    private readonly AppSettings _settings;
    private readonly UISettings _systemSettings = new();
    private bool _closed;

    public WindowThemeController(Window window, FrameworkElement root, AppSettings settings)
    {
        _window = window;
        _root = root;
        _settings = settings;
        settings.ThemeChanged += OnThemeChanged;
        _systemSettings.ColorValuesChanged += OnSystemColorsChanged;
        window.Closed += OnClosed;
        Apply();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => Apply();

    private void OnSystemColorsChanged(UISettings sender, object args)
    {
        // Windows raises this on a worker thread; all XAML changes belong on the UI thread.
        _root.DispatcherQueue.TryEnqueue(() =>
        {
            if (!_closed && _settings.Theme == AppTheme.System)
            {
                Apply();
            }
        });
    }

    private void Apply()
    {
        var dark = _settings.Theme switch
        {
            AppTheme.Light => false,
            AppTheme.Dark => true,
            _ => _systemSettings.GetColorValue(UIColorType.Background) == Colors.Black,
        };
        _root.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
        var titleBar = _window.AppWindow.TitleBar;
        titleBar.ButtonForegroundColor = dark ? Colors.White : Colors.Black;
        titleBar.ButtonInactiveForegroundColor = Colors.Gray;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _closed = true;
        _settings.ThemeChanged -= OnThemeChanged;
        _systemSettings.ColorValuesChanged -= OnSystemColorsChanged;
        _window.Closed -= OnClosed;
    }
}
