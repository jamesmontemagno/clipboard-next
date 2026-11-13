namespace ClipboardNext.Core;

/// <summary>
/// Decides which key presses count as "the user just pasted" on Windows.
/// Kept apart from the keyboard hook so the rules can be tested.
/// </summary>
public static class PasteGesture
{
    public const int VirtualKeyV = 0x56;
    public const int VirtualKeyInsert = 0x2D;

    /// <summary>Whether a key can start a paste gesture at all. Every other key is ignored unread.</summary>
    public static bool IsTriggerKey(int virtualKey) => virtualKey is VirtualKeyV or VirtualKeyInsert;

    /// <summary>
    /// Whether a trigger key, with these modifiers held, is a paste shortcut.
    /// Ctrl+V always is. Ctrl+Shift+V and Shift+Insert, the terminal shortcuts, are when
    /// <paramref name="includeTerminalShortcuts"/> is on. Alt (including AltGr, which Windows
    /// reports as Ctrl+Alt) and the Windows key never are: Win+V opens clipboard history
    /// and Ctrl+Alt+V is Paste Special.
    /// </summary>
    public static bool IsPaste(
        int virtualKey,
        bool control,
        bool shift,
        bool alt,
        bool windows,
        bool includeTerminalShortcuts)
    {
        if (alt || windows)
        {
            return false;
        }

        return virtualKey switch
        {
            VirtualKeyV => control && (!shift || includeTerminalShortcuts),
            VirtualKeyInsert => includeTerminalShortcuts && shift && !control,
            _ => false,
        };
    }
}
