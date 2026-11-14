using ClipboardNext.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using WinRT.Interop;

namespace ClipboardNext.App;

/// <summary>
/// The always-on-top title and progress badge. It never takes keyboard focus,
/// can be dragged anywhere on any display, and remembers where it was left.
/// </summary>
public sealed partial class BadgeWindow : Window
{
    private readonly AppModel _model;
    private readonly nint _hwnd;
    private readonly FloatingWindowDragger _dragger;
    private XamlRoot? _xamlRoot;
    private int _widthDip = BadgePlacement.WidthDip;
    private bool _hasPlacement;

    internal BadgeWindow(AppModel model)
    {
        _model = model;
        InitializeComponent();
        Title = "Clipboard Next badge";
        _hwnd = WindowNative.GetWindowHandle(this);

        var presenter = OverlappedPresenter.CreateForContextMenu();
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        NativeWindow.MakeNonActivatingToolWindow(_hwnd);
        NativeWindow.RoundCorners(_hwnd);

        _dragger = new FloatingWindowDragger(AppWindow);
        _dragger.Moved += OnMoved;
        _model.Queue.Changed += OnStateChanged;
        _model.Flash.Changed += OnStateChanged;
        RootGrid.Loaded += OnLoaded;
        Closed += OnClosed;
        Refresh();
    }

    internal void ShowBadge()
    {
        Place();
        AppWindow.Show(activateWindow: false);
    }

    /// <summary>Moves the badge to its remembered spot, or to the default when that is off screen.</summary>
    internal void Place()
    {
        // Size for the display the window is on, move, then size again: the display it lands on
        // can have a different scale, and the default position depends on the badge's width.
        var target = Resolve(AppWindowPlacement.GetScaleForWindow(_hwnd));
        AppWindow.Move(new PointInt32(target.X, target.Y));
        target = Resolve(AppWindowPlacement.GetScaleForWindow(_hwnd));
        if (_widthDip == BadgePlacement.TitledWidthDip)
        {
            target = ClampToCurrentWorkArea(target);
        }

        AppWindow.MoveAndResize(target);
        _hasPlacement = true;
        RememberAdjustedPosition();
    }

    private RectInt32 Resolve(double scale)
    {
        var width = AppWindowPlacement.DipToPixels(_widthDip, scale);
        var height = AppWindowPlacement.DipToPixels(BadgePlacement.HeightDip, scale);
        var margin = AppWindowPlacement.DipToPixels(BadgePlacement.MarginDip, scale);

        // Indexed on purpose: enumerating DisplayArea.FindAll() with foreach can throw.
        var displays = DisplayArea.FindAll();
        var workAreas = new List<PixelRect>(displays.Count);
        for (var i = 0; i < displays.Count; i++)
        {
            workAreas.Add(AppWindowPlacement.ToPixelRect(displays[i].WorkArea));
        }

        var primary = AppWindowPlacement.ToPixelRect(DisplayArea.Primary.WorkArea);
        var position = BadgePlacement.Resolve(_model.Settings.BadgePosition, width, height, margin, primary, workAreas);
        return new RectInt32(position.X, position.Y, width, height);
    }

    private void Refresh()
    {
        var queue = _model.Queue;
        var detected = _model.Flash.IsActive;
        var running = queue.Status == QueueStatus.Running;
        var title = QueuePresentation.BadgeTitle(queue);
        var hasTitle = title.Length > 0;

        TitleText.Text = title;
        TitleText.Visibility = hasTitle ? Visibility.Visible : Visibility.Collapsed;
        TitleSeparator.Visibility = hasTitle ? Visibility.Visible : Visibility.Collapsed;
        TitleColumn.Width = hasTitle ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        BadgeContent.HorizontalAlignment = hasTitle ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        PositionText.Text = QueuePresentation.BadgeText(queue);
        ClipboardIcon.Visibility = detected ? Visibility.Collapsed : Visibility.Visible;
        DetectedIcon.Visibility = detected ? Visibility.Visible : Visibility.Collapsed;
        RestingFrame.Visibility = detected ? Visibility.Collapsed : Visibility.Visible;
        DetectedFrame.Visibility = detected ? Visibility.Visible : Visibility.Collapsed;
        IdleDot.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
        RunningDot.Visibility = running ? Visibility.Visible : Visibility.Collapsed;

        var state = detected ? "Paste detected" : queue.Status.DisplayName();
        var description = hasTitle ? $"{title}, {queue.Progress}, {state}" : $"{queue.Progress}, {state}";
        ToolTipService.SetToolTip(RootGrid, $"{description}. Drag to move. Hide it from the tray menu or Settings.");
        AutomationProperties.SetName(RootGrid, description);

        var widthDip = hasTitle ? BadgePlacement.TitledWidthDip : BadgePlacement.WidthDip;
        if (_widthDip != widthDip)
        {
            _widthDip = widthDip;
            ResizeForScale(AppWindowPlacement.GetScaleForWindow(_hwnd));
        }
    }

    private void OnStateChanged(object? sender, EventArgs e) => Refresh();

    private void OnMoved(object? sender, EventArgs e)
    {
        var position = AppWindow.Position;
        _model.Settings.BadgePosition = (position.X, position.Y);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RootGrid.Loaded -= OnLoaded;
        _xamlRoot = RootGrid.XamlRoot;
        if (_xamlRoot is not null)
        {
            _xamlRoot.Changed += OnXamlRootChanged;
        }
    }

    // Dragging the badge to a display with another scale keeps its size in DIPs.
    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
        => ResizeForScale(sender.RasterizationScale);

    private void ResizeForScale(double scale)
    {
        if (!_hasPlacement)
        {
            return;
        }

        var width = AppWindowPlacement.DipToPixels(_widthDip, scale);
        var height = AppWindowPlacement.DipToPixels(BadgePlacement.HeightDip, scale);
        if (AppWindow.Size.Width != width || AppWindow.Size.Height != height)
        {
            var position = AppWindow.Position;
            AppWindow.MoveAndResize(ClampToCurrentWorkArea(new RectInt32(position.X, position.Y, width, height)));
            RememberAdjustedPosition();
        }
    }

    private RectInt32 ClampToCurrentWorkArea(RectInt32 bounds) =>
        AppWindowPlacement.ClampToWorkArea(
            DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea,
            bounds.X, bounds.Y, bounds.Width, bounds.Height);

    private void RememberAdjustedPosition()
    {
        var position = AppWindow.Position;
        if (_model.Settings.BadgePosition is { } saved && saved != (position.X, position.Y))
        {
            _model.Settings.BadgePosition = (position.X, position.Y);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _dragger.Moved -= OnMoved;
        _model.Queue.Changed -= OnStateChanged;
        _model.Flash.Changed -= OnStateChanged;
        RootGrid.Loaded -= OnLoaded;
        if (_xamlRoot is not null)
        {
            _xamlRoot.Changed -= OnXamlRootChanged;
            _xamlRoot = null;
        }

        Closed -= OnClosed;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e) => _dragger.OnPointerPressed(sender, e);

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e) => _dragger.OnPointerMoved(sender, e);

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e) => _dragger.OnPointerReleased(sender, e);

    private void OnPointerCaptureEnded(object sender, PointerRoutedEventArgs e) => _dragger.OnPointerCaptureEnded(sender, e);
}
