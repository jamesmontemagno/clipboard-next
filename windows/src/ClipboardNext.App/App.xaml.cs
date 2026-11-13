using System.Diagnostics;
using ClipboardNext.Core;
using H.NotifyIcon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AppLifecycle;
using WinRT.Interop;

namespace ClipboardNext.App;

/// <summary>
/// Clipboard Next for Windows: a tray app. The tray icon and its popup are always there; the
/// editor window opens on a normal launch and can be closed without ending the app.
/// </summary>
public partial class App : Application
{
    private static readonly TimeSpan TrayIconInitialRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TrayIconMaxRetryDelay = TimeSpan.FromSeconds(40);
    private const int TrayIconMaxRetryAttempts = 6; // 5 + 10 + 20 + 40 + 40 + 40 s, about 2.5 min

    private DispatcherQueue? _dispatcher;
    private AppModel? _model;
    private TaskbarIcon? _taskbarIcon;
    private DispatcherQueueTimer? _trayIconRetryTimer;
    private int _trayIconRetryAttempts;
    private BitmapImage? _idleIcon;
    private BitmapImage? _runningIcon;
    private BitmapImage? _detectedIcon;
    private BitmapImage? _shownIcon;
    private TrayPopupView? _trayView;
    private TrayPopupWindow? _trayPopup;
    private EditorWindow? _editorWindow;
    private bool _isExiting;
    private bool _isInStartupPhase = true;

    public App()
    {
        InitializeComponent();
        RegisterGlobalExceptionHandlers();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // This is a tray app: closing its last window must not end the process. The only way out
        // is ExitApplication, which exits explicitly.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;

        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // Nothing may escape OnLaunched: XAML treats an exception here as fatal and terminates the
        // process with a stowed exception (0xC000027B) even when Application.UnhandledException
        // marks it handled. Every step is therefore guarded. The queue and the tray icon are the
        // app; everything else is best-effort.
        RunStartupStep(nameof(CreateModel), CreateModel);
        if (_model is null)
        {
            // Without the queue there is nothing to show; do not linger as an invisible process.
            ExitApplication();
            return;
        }

        RunStartupStep(nameof(CreateTrayIcon), CreateTrayIcon);
        RunStartupStep(nameof(StartMonitoring), StartMonitoring);
        RunStartupStep(nameof(OpenEditorOnLaunch), OpenEditorOnLaunch);
        // From here on, later launches are forwarded to this process instead of starting another.
        RunStartupStep(nameof(SingleInstance), () => SingleInstance.SetActivationHandler(OnRedirectedActivation));
        RunStartupStep(nameof(EndStartupPhaseAfterFirstDispatcherPass), EndStartupPhaseAfterFirstDispatcherPass);
    }

    private static void RunStartupStep(string name, Action step)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Startup step {name} failed: {ex}");
            CrashDiagnostics.Log($"Startup.{name}", ex, handled: true);
        }
    }

    private void EndStartupPhaseAfterFirstDispatcherPass()
    {
        var dispatcher = _dispatcher;
        if (dispatcher is null || !dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () => _isInStartupPhase = false))
        {
            _isInStartupPhase = false;
        }
    }

    private void RegisterGlobalExceptionHandlers()
    {
        // During launch, log and swallow so a non-fatal startup hiccup cannot take down a tray app
        // that has no main window. After launch the handler only records diagnostics: continuing
        // past an arbitrary mid-operation failure could leave the queue half-changed.
        UnhandledException += (_, e) =>
        {
            var handled = _isInStartupPhase && !_isExiting;
            Debug.WriteLine($"Unhandled XAML exception (startup={_isInStartupPhase}): {e.Exception}");
            CrashDiagnostics.Log("Application.UnhandledException", e.Exception, handled);
            e.Handled = handled;
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Debug.WriteLine($"Unobserved task exception: {e.Exception}");
            CrashDiagnostics.Log("TaskScheduler.UnobservedTaskException", e.Exception, handled: true);
            e.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Debug.WriteLine($"Unhandled AppDomain exception (terminating={e.IsTerminating}): {e.ExceptionObject}");
            CrashDiagnostics.Log("AppDomain.UnhandledException", e.ExceptionObject, handled: false);
        };
    }

    private void CreateModel()
    {
        _model = new AppModel(_dispatcher!);
        _model.Queue.Changed += OnTrayStateChanged;
        _model.Flash.Changed += OnTrayStateChanged;
    }

    private void StartMonitoring()
    {
        if (_model is not { } model)
        {
            return;
        }

        // Restoring never arms the queue or touches the clipboard; it only brings back the
        // user's choices: paste detection, if they turned it on, and the badge, if it was showing.
        model.Monitor.Start();
        model.Badge.Restore();
    }

    // A normal launch shows the editor. Signing in with "Start with Windows" on stays in the tray.
    private void OpenEditorOnLaunch()
    {
        if (SingleInstance.CurrentActivationKind() != ExtendedActivationKind.StartupTask)
        {
            OpenEditor();
        }
    }

    // Runs on a background thread, in the middle of another launch handing over its activation.
    private void OnRedirectedActivation(ExtendedActivationKind kind)
    {
        if (kind != ExtendedActivationKind.StartupTask)
        {
            _dispatcher?.TryEnqueue(OpenEditor);
        }
    }

    private void CreateTrayIcon()
    {
        if (_taskbarIcon is not null || _model is not { } model)
        {
            return;
        }

        _idleIcon = new BitmapImage(new Uri("ms-appx:///Assets/TrayIcon.ico"));
        _runningIcon = new BitmapImage(new Uri("ms-appx:///Assets/TrayIconRunning.ico"));
        _detectedIcon = new BitmapImage(new Uri("ms-appx:///Assets/TrayIconDetected.ico"));
        _shownIcon = _idleIcon;

        _trayView = new TrayPopupView(model, OpenEditor, ExitApplication, () => _trayPopup?.Hide());

        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = QueuePresentation.TrayToolTip(model.Queue),
            IconSource = _idleIcon,
            NoLeftClickDelay = true,
        };

        // The app hosts its own popup window rather than a context flyout, so both mouse buttons
        // open it next to the tray icon.
        var showPopup = new RelayCommand(ShowTrayPopup);
        _taskbarIcon.LeftClickCommand = showPopup;
        _taskbarIcon.RightClickCommand = showPopup;

        if (!TryRegisterTrayIconWithShell())
        {
            ScheduleTrayIconRetry();
        }
    }

    /// <summary>
    /// Asks the shell to add the notification icon. Returns <see langword="false"/> instead of
    /// throwing when <c>Shell_NotifyIcon</c> refuses (no taskbar yet); the <see cref="TaskbarIcon"/>
    /// stays alive so a later attempt, or the shell's own <c>TaskbarCreated</c> broadcast, can add it.
    /// </summary>
    private bool TryRegisterTrayIconWithShell()
    {
        if (_taskbarIcon is null || _isExiting || _taskbarIcon.IsCreated)
        {
            return true;
        }

        try
        {
            // The default also puts the whole process in Efficiency Mode (idle priority and
            // EcoQoS) and nothing restores it, which would delay the queue's paste timing.
            _taskbarIcon.ForceCreate(enablesEfficiencyMode: false);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Tray icon registration failed (attempt {_trayIconRetryAttempts + 1}): {ex.Message}");
            if (_trayIconRetryAttempts == 0)
            {
                CrashDiagnostics.Log("TaskbarIcon.ForceCreate", ex, handled: true);
            }

            return false;
        }
    }

    private void ScheduleTrayIconRetry()
    {
        if (_trayIconRetryTimer is not null || _dispatcher is null || _isExiting)
        {
            return;
        }

        _trayIconRetryTimer = _dispatcher.CreateTimer();
        _trayIconRetryTimer.Interval = TrayIconInitialRetryDelay;
        _trayIconRetryTimer.IsRepeating = false;
        _trayIconRetryTimer.Tick += OnTrayIconRetryTick;
        _trayIconRetryTimer.Start();
    }

    private void OnTrayIconRetryTick(DispatcherQueueTimer sender, object args)
    {
        _trayIconRetryAttempts++;
        if (!TryRegisterTrayIconWithShell() && _trayIconRetryAttempts < TrayIconMaxRetryAttempts && !_isExiting)
        {
            // Exponential backoff, capped: the next delay doubles until TrayIconMaxRetryDelay.
            sender.Interval = TimeSpan.FromTicks(Math.Min(sender.Interval.Ticks * 2, TrayIconMaxRetryDelay.Ticks));
            sender.Start();
            return;
        }

        if (_taskbarIcon is { IsCreated: false })
        {
            // Give up polling; H.NotifyIcon still listens for TaskbarCreated, and the editor keeps
            // the app usable. Recorded once so a persistent failure leaves evidence in crash.log.
            CrashDiagnostics.Log(
                "TaskbarIcon.ForceCreate",
                new InvalidOperationException($"Tray icon still not registered after {_trayIconRetryAttempts} attempts; waiting for TaskbarCreated."),
                handled: true);
        }

        StopTrayIconRetry();
    }

    private void StopTrayIconRetry()
    {
        if (_trayIconRetryTimer is null)
        {
            return;
        }

        _trayIconRetryTimer.Stop();
        _trayIconRetryTimer.Tick -= OnTrayIconRetryTick;
        _trayIconRetryTimer = null;
    }

    // The tray icon is the Windows stand-in for the macOS menu bar label: its tooltip carries the
    // progress, a dot marks a running queue, and a checkmark flashes when a paste is detected.
    private void OnTrayStateChanged(object? sender, EventArgs e)
    {
        if (_taskbarIcon is not { } icon || _model is not { } model || _isExiting)
        {
            return;
        }

        icon.ToolTipText = QueuePresentation.TrayToolTip(model.Queue);
        var wanted = model.Flash.IsActive ? _detectedIcon
            : model.Queue.Status == QueueStatus.Running ? _runningIcon
            : _idleIcon;
        if (wanted is not null && !ReferenceEquals(wanted, _shownIcon))
        {
            _shownIcon = wanted;
            icon.IconSource = wanted;
        }
    }

    private void ShowTrayPopup()
    {
        if (_isExiting || _trayView is null)
        {
            return;
        }

        // The content is prepared ahead of time, but no window is created until it is requested:
        // a hidden acrylic window can otherwise leave an empty surface at startup.
        _trayPopup ??= new TrayPopupWindow(_trayView);
        _trayPopup.ShowNearCursor(TrayPopupView.PopupWidth, TrayPopupView.PopupHeight);
    }

    private void OpenEditor()
    {
        if (_isExiting || _model is not { } model)
        {
            return;
        }

        if (_editorWindow is null)
        {
            var window = new EditorWindow(model);
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(_editorWindow, window))
                {
                    _editorWindow = null;
                }
            };
            _editorWindow = window;
        }

        if (_editorWindow.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }

        _editorWindow.Activate();
        NativeWindow.BringToForeground(WindowNative.GetWindowHandle(_editorWindow));
    }

    /// <summary>
    /// Quits the app. Like the macOS app, it stops the queue but leaves the clipboard as it is.
    /// </summary>
    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        try
        {
            _model?.Queue.Pause();
            StopTrayIconRetry();
            _taskbarIcon?.Dispose();
            _taskbarIcon = null;
            // The tray icon is gone; let a new launch start fresh from here on.
            SingleInstance.Release();
            _editorWindow?.Close();
            _trayPopup?.Close();
            _model?.Dispose();
        }
        catch (Exception ex)
        {
            CrashDiagnostics.Log(nameof(ExitApplication), ex, handled: true);
        }

        Exit();
        // No persistent host window keeps the process alive, so force termination to guarantee
        // the user can always quit from the tray.
        Environment.Exit(0);
    }
}
