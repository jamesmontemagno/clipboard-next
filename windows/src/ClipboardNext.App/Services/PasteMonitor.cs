using System.Runtime.InteropServices;
using ClipboardNext.Core;
using Microsoft.UI.Dispatching;

namespace ClipboardNext.App;

/// <summary>
/// Detects the paste shortcut system-wide with a listen-only low-level keyboard hook, the Windows
/// counterpart of the macOS app's event tap. It never blocks, changes, or records a keystroke:
/// every key is passed on untouched, keys are only examined while a queue is running, and of
/// those only V and Insert are looked at, to tell the queue "a paste just happened".
/// <para>
/// The hook lives on its own thread with its own message loop. Windows calls a low-level hook
/// synchronously for every keystroke in every app, so it must never wait on the UI thread.
/// </para>
/// <para>
/// Windows does not deliver keystrokes typed into an elevated (administrator) window to a
/// non-elevated app, so pastes there are not detected; Next still works.
/// </para>
/// </summary>
internal sealed class PasteMonitor : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int HC_ACTION = 0;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const uint WM_QUIT = 0x0012;
    private const uint PM_NOREMOVE = 0x0000;
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    private readonly DemoQueue _queue;
    private readonly AppSettings _settings;
    private readonly DispatcherQueue _dispatcher;
    private readonly uint _processId = (uint)Environment.ProcessId;
    private readonly ManualResetEventSlim _started = new(false);

    private Thread? _thread;
    private HookProc? _hookProc; // held to keep the native callback alive
    private uint _threadId;
    private bool _hookInstalled;
    private int _hookError;
    private int _armed;
    private int _terminalShortcuts;

    // Touched only on the hook thread.
    private bool _vDown;
    private bool _insertDown;
    private int _gestureKey;

    public PasteMonitor(DemoQueue queue, AppSettings settings, DispatcherQueue dispatcher)
    {
        _queue = queue;
        _settings = settings;
        _dispatcher = dispatcher;
        _terminalShortcuts = settings.DetectTerminalPasteShortcuts ? 1 : 0;
        _queue.Changed += OnQueueChanged;
    }

    private delegate nint HookProc(int code, nint wParam, nint lParam);

    /// <summary>Raised on the UI thread when <see cref="IsMonitoring"/> or <see cref="Error"/> changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Whether the keyboard hook is installed, so pastes advance the queue automatically.</summary>
    public bool IsMonitoring { get; private set; }

    public string? Error { get; private set; }

    /// <summary>Whether Ctrl+Shift+V and Shift+Insert also count as a paste.</summary>
    public bool DetectTerminalShortcuts
    {
        get => _settings.DetectTerminalPasteShortcuts;
        set
        {
            _settings.DetectTerminalPasteShortcuts = value;
            Volatile.Write(ref _terminalShortcuts, value ? 1 : 0);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Starts monitoring at launch if the user turned paste detection on earlier.</summary>
    public void Start()
    {
        if (_settings.PasteDetectionEnabled)
        {
            Install();
        }
    }

    /// <summary>Turns paste detection on and remembers the choice. Also retries after a failure.</summary>
    public void Enable()
    {
        _settings.PasteDetectionEnabled = true;
        Install();
    }

    /// <summary>Turns paste detection off and remembers the choice. Next still advances manually.</summary>
    public void Disable()
    {
        _settings.PasteDetectionEnabled = false;
        Uninstall();
        Error = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _queue.Changed -= OnQueueChanged;
        Uninstall();
    }

    private void OnQueueChanged(object? sender, EventArgs e) =>
        Volatile.Write(ref _armed, _queue.Status == QueueStatus.Running ? 1 : 0);

    private void Install()
    {
        if (_thread is not null)
        {
            return;
        }

        _started.Reset();
        _hookInstalled = false;
        _hookError = 0;
        var thread = new Thread(HookThread)
        {
            IsBackground = true,
            Name = "ClipboardNext.PasteMonitor",
        };
        thread.Start();

        if (_started.Wait(StartTimeout) && _hookInstalled)
        {
            _thread = thread;
            IsMonitoring = true;
            Error = null;
            Volatile.Write(ref _armed, _queue.Status == QueueStatus.Running ? 1 : 0);
        }
        else
        {
            // A thread that has not reported back may still install its hook later; stop it.
            if (thread.IsAlive && _threadId != 0)
            {
                PostThreadMessageW(_threadId, WM_QUIT, 0, 0);
            }

            IsMonitoring = false;
            Error = _hookError != 0
                ? $"Paste detection could not start (Windows error {_hookError}). Use Next to advance, or try again."
                : "Paste detection could not start. Use Next to advance, or try again.";
            if (_queue.Status == QueueStatus.Running)
            {
                _queue.Pause(Error);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Uninstall()
    {
        var thread = _thread;
        if (thread is null)
        {
            return;
        }

        _thread = null;
        IsMonitoring = false;
        Volatile.Write(ref _armed, 0);
        if (thread.IsAlive)
        {
            PostThreadMessageW(_threadId, WM_QUIT, 0, 0);
            thread.Join(StopTimeout);
        }
    }

    private void HookThread()
    {
        nint hook = 0;
        try
        {
            _threadId = GetCurrentThreadId();
            // Force the thread's message queue into existence so WM_QUIT can be posted to it.
            PeekMessageW(out _, 0, 0, 0, PM_NOREMOVE);
            _hookProc = OnKeyboardEvent;
            hook = SetWindowsHookExW(WH_KEYBOARD_LL, _hookProc, GetModuleHandleW(null), 0);
            if (hook == 0)
            {
                _hookError = Marshal.GetLastWin32Error();
            }

            _hookInstalled = hook != 0;
        }
        catch (Exception ex)
        {
            CrashDiagnostics.Log(nameof(PasteMonitor), ex, handled: true);
        }
        finally
        {
            _started.Set();
        }

        if (hook == 0)
        {
            return;
        }

        while (GetMessageW(out var message, 0, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessageW(ref message);
        }

        UnhookWindowsHookEx(hook);
    }

    private nint OnKeyboardEvent(int code, nint wParam, nint lParam)
    {
        if (code == HC_ACTION)
        {
            try
            {
                // KBDLLHOOKSTRUCT starts with the virtual-key code.
                Examine((int)wParam, Marshal.ReadInt32(lParam));
            }
            catch (Exception ex)
            {
                CrashDiagnostics.Log(nameof(PasteMonitor), ex, handled: true);
            }
        }

        return CallNextHookEx(0, code, wParam, lParam);
    }

    private void Examine(int message, int virtualKey)
    {
        if (Volatile.Read(ref _armed) == 0)
        {
            _vDown = false;
            _insertDown = false;
            _gestureKey = 0;
            return;
        }

        if (!PasteGesture.IsTriggerKey(virtualKey))
        {
            return;
        }

        ref var down = ref (virtualKey == PasteGesture.VirtualKeyV ? ref _vDown : ref _insertDown);
        if (message is WM_KEYDOWN or WM_SYSKEYDOWN)
        {
            // The hook reports no repeat flag: a key-down for a key that is already down is one.
            var isRepeat = down;
            down = true;
            var isPaste = PasteGesture.IsPaste(
                virtualKey,
                control: IsKeyDown(VK_CONTROL),
                shift: IsKeyDown(VK_SHIFT),
                alt: IsKeyDown(VK_MENU),
                windows: IsKeyDown(VK_LWIN) || IsKeyDown(VK_RWIN),
                includeTerminalShortcuts: Volatile.Read(ref _terminalShortcuts) != 0);
            if (!isPaste)
            {
                return;
            }

            _gestureKey = virtualKey;
            var isOwnApp = IsOwnAppInForeground();
            _dispatcher.TryEnqueue(() => _queue.KeyDown(isPasteGesture: true, isRepeat, isOwnApp));
        }
        else if (message is WM_KEYUP or WM_SYSKEYUP)
        {
            down = false;
            if (_gestureKey == virtualKey)
            {
                _gestureKey = 0;
                _dispatcher.TryEnqueue(() => _queue.KeyUp());
            }
        }
    }

    private static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private bool IsOwnAppInForeground()
    {
        var foreground = GetForegroundWindow();
        if (foreground == 0)
        {
            return false;
        }

        GetWindowThreadProcessId(foreground, out var processId);
        return processId == _processId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookExW(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DispatchMessageW(ref MSG lpMsg);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessageW(uint idThread, uint msg, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? lpModuleName);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
