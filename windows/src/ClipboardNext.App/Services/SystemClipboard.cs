using System.Runtime.InteropServices;
using ClipboardNext.Core;

namespace ClipboardNext.App;

/// <summary>
/// The Windows clipboard, through the Win32 API. Text is placed directly rather than through a
/// WinRT <c>DataPackage</c>: a data package renders its content lazily, which moves the clipboard
/// sequence number again after the write and would look like another app taking the clipboard.
/// Use from the UI thread, which owns the hidden window the clipboard is opened with.
/// </summary>
internal sealed class SystemClipboard : IClipboardAccess, IDisposable
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;
    private const int OpenAttempts = 10;
    private static readonly nint HWND_MESSAGE = new(-3);

    // Opening the clipboard without a window makes EmptyClipboard leave it without an owner,
    // which Windows documents as making SetClipboardData fail. A message-only window owns it.
    private nint _owner = CreateWindowExW(0, "Static", string.Empty, 0, 0, 0, 0, 0, HWND_MESSAGE, 0, 0, 0);

    public long ChangeCount => GetClipboardSequenceNumber();

    public void Write(string text)
    {
        // CF_UNICODETEXT lines end in CR LF; queue files keep the portable LF.
        var data = text.ReplaceLineEndings("\r\n");
        var memory = GlobalAlloc(GMEM_MOVEABLE, (nuint)((data.Length + 1) * sizeof(char)));
        if (memory == 0)
        {
            throw new ClipboardWriteException();
        }

        try
        {
            var target = GlobalLock(memory);
            if (target == 0)
            {
                throw new ClipboardWriteException();
            }

            try
            {
                Marshal.Copy(data.ToCharArray(), 0, target, data.Length);
                Marshal.WriteInt16(target, data.Length * sizeof(char), 0);
            }
            finally
            {
                GlobalUnlock(memory);
            }

            if (!TryOpen())
            {
                throw new ClipboardWriteException();
            }

            try
            {
                if (!EmptyClipboard() || SetClipboardData(CF_UNICODETEXT, memory) == 0)
                {
                    throw new ClipboardWriteException();
                }

                // The clipboard owns the memory from here on.
                memory = 0;
            }
            finally
            {
                CloseClipboard();
            }
        }
        finally
        {
            if (memory != 0)
            {
                GlobalFree(memory);
            }
        }
    }

    public string? ReadText()
    {
        if (!IsClipboardFormatAvailable(CF_UNICODETEXT) || !TryOpen())
        {
            return null;
        }

        try
        {
            var memory = GetClipboardData(CF_UNICODETEXT);
            if (memory == 0)
            {
                return null;
            }

            var source = GlobalLock(memory);
            if (source == 0)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni(source);
            }
            finally
            {
                GlobalUnlock(memory);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    public void Dispose()
    {
        if (_owner != 0)
        {
            DestroyWindow(_owner);
            _owner = 0;
        }
    }

    // Another process can hold the clipboard open for a moment; retry briefly before giving up.
    private bool TryOpen()
    {
        for (var attempt = 0; attempt < OpenAttempts; attempt++)
        {
            if (OpenClipboard(_owner))
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return false;
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetClipboardData(uint uFormat, nint hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetClipboardData(uint uFormat);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalFree(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalLock(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(nint hMem);
}
