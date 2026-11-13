namespace ClipboardNext.Core;

public enum QueueStatus
{
    Idle,
    Running,
    Paused,
    Finished,
}

public static class QueueStatusExtensions
{
    /// <summary>The label shown in the tray popup, editor, and badge.</summary>
    public static string DisplayName(this QueueStatus status) => status switch
    {
        QueueStatus.Running => "Running",
        QueueStatus.Paused => "Paused",
        QueueStatus.Finished => "Finished",
        _ => "Ready",
    };
}
