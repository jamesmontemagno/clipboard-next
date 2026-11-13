namespace ClipboardNext.Core;

public enum QueuePrimaryAction
{
    Start,
    Pause,
    Resume,
}

/// <summary>
/// What the tray popup, editor, badge, and tray icon show for a queue's state, in one place
/// so every surface agrees.
/// </summary>
public static class QueuePresentation
{
    public const string AppName = "Clipboard Next";

    /// <summary>How long the "paste detected" checkmark stays up.</summary>
    public static readonly TimeSpan PasteFlashDuration = TimeSpan.FromMilliseconds(900);

    /// <summary>The middle transport button: Pause while running, Resume while paused, otherwise Start.</summary>
    public static QueuePrimaryAction PrimaryAction(DemoQueue queue) => queue.Status switch
    {
        QueueStatus.Running => QueuePrimaryAction.Pause,
        QueueStatus.Paused => QueuePrimaryAction.Resume,
        _ => QueuePrimaryAction.Start,
    };

    public static bool CanGoNext(DemoQueue queue) =>
        queue.Snippets.Count > 0 && queue.Status != QueueStatus.Finished;

    /// <summary>The badge's compact position, such as <c>3/12</c>.</summary>
    public static string BadgeText(DemoQueue queue) =>
        $"{(queue.Index is { } index ? index + 1 : 0)}/{queue.Snippets.Count}";

    public static string BadgeTitle(DemoQueue queue) =>
        string.Join(" ", (queue.Current?.Title ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>The tray icon's tooltip: progress while running, otherwise the status.</summary>
    public static string TrayToolTip(DemoQueue queue) => queue.Status == QueueStatus.Running
        ? $"{AppName} · {queue.Progress}"
        : $"{AppName} · {queue.Status.DisplayName()}";
}
