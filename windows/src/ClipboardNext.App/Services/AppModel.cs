using ClipboardNext.Core;
using Microsoft.UI.Dispatching;

namespace ClipboardNext.App;

/// <summary>
/// The app's long-lived state: the queue, its settings, paste detection, and the floating badge.
/// Created once on the UI thread and shared by the tray popup, the editor, and the badge.
/// </summary>
internal sealed class AppModel : IDisposable
{
    private static readonly TimeSpan ClipboardPollInterval = TimeSpan.FromMilliseconds(200);

    private readonly DispatcherQueue _dispatcher;
    private readonly SystemClipboard _clipboard = new();
    private readonly DispatcherQueueTimer _clipboardTimer;

    public AppModel(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        Settings = new AppSettings(AppPaths.SettingsPath);
        Queue = new DemoQueue(_clipboard, AppPaths.QueuePath, Settings, Schedule);
        Monitor = new PasteMonitor(Queue, Settings, dispatcher);
        Flash = new PasteFlash(Queue, dispatcher);
        Badge = new FloatingBadge(this);

        _clipboardTimer = dispatcher.CreateTimer();
        _clipboardTimer.Interval = ClipboardPollInterval;
        _clipboardTimer.Tick += (_, _) => Queue.CheckClipboard();
        Queue.Changed += OnQueueChanged;
    }

    public AppSettings Settings { get; }

    public DemoQueue Queue { get; }

    public PasteMonitor Monitor { get; }

    public PasteFlash Flash { get; }

    public FloatingBadge Badge { get; }

    public void Dispose()
    {
        Queue.Changed -= OnQueueChanged;
        _clipboardTimer.Stop();
        Monitor.Dispose();
        Badge.Close();
        _clipboard.Dispose();
    }

    // Watching for another app's copy only matters while the queue's snippet is on the clipboard.
    private void OnQueueChanged(object? sender, EventArgs e)
    {
        if (!Queue.OwnsClipboard)
        {
            _clipboardTimer.Stop();
        }
        else if (!_clipboardTimer.IsRunning)
        {
            _clipboardTimer.Start();
        }
    }

    private Action Schedule(int milliseconds, Action action)
    {
        var timer = _dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(milliseconds);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => action();
        timer.Start();
        return timer.Stop;
    }
}

/// <summary>
/// The brief "paste detected" confirmation shown by the tray icon and the badge: on for
/// <see cref="QueuePresentation.PasteFlashDuration"/> after each detected paste.
/// </summary>
internal sealed class PasteFlash
{
    private readonly DemoQueue _queue;
    private readonly DispatcherQueueTimer _timer;
    private int _seenPastes;

    public PasteFlash(DemoQueue queue, DispatcherQueue dispatcher)
    {
        _queue = queue;
        _seenPastes = queue.DetectedPastes;
        _timer = dispatcher.CreateTimer();
        _timer.Interval = QueuePresentation.PasteFlashDuration;
        _timer.IsRepeating = false;
        _timer.Tick += (_, _) =>
        {
            IsActive = false;
            Changed?.Invoke(this, EventArgs.Empty);
        };
        queue.Changed += OnQueueChanged;
    }

    public event EventHandler? Changed;

    public bool IsActive { get; private set; }

    private void OnQueueChanged(object? sender, EventArgs e)
    {
        if (_queue.DetectedPastes == _seenPastes)
        {
            return;
        }

        _seenPastes = _queue.DetectedPastes;
        IsActive = true;
        _timer.Stop();
        _timer.Start();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>The optional always-on-top progress badge: whether it is shown, and where.</summary>
internal sealed class FloatingBadge
{
    private readonly AppModel _model;
    private BadgeWindow? _window;

    public FloatingBadge(AppModel model)
    {
        _model = model;
    }

    public event EventHandler? Changed;

    public bool IsVisible
    {
        get => _model.Settings.ShowFloatingBadge;
        set
        {
            if (value == _model.Settings.ShowFloatingBadge)
            {
                return;
            }

            _model.Settings.ShowFloatingBadge = value;
            if (value)
            {
                Show();
            }
            else
            {
                _window?.AppWindow.Hide();
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Shows the badge at launch if it was showing when the app last ran.</summary>
    public void Restore()
    {
        if (IsVisible)
        {
            Show();
        }
    }

    /// <summary>Puts the badge back in the top-right corner of the primary display.</summary>
    public void ResetPosition()
    {
        _model.Settings.BadgePosition = null;
        _window?.Place();
    }

    public void Close()
    {
        _window?.Close();
        _window = null;
    }

    private void Show()
    {
        _window ??= new BadgeWindow(_model);
        _window.ShowBadge();
    }
}
