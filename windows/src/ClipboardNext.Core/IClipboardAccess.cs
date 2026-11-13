namespace ClipboardNext.Core;

/// <summary>The system clipboard as the queue needs it. Faked in tests.</summary>
public interface IClipboardAccess
{
    /// <summary>A number that changes whenever anything writes to the clipboard.</summary>
    long ChangeCount { get; }

    /// <summary>Replaces the clipboard with plain text. Throws <see cref="ClipboardWriteException"/> on failure.</summary>
    void Write(string text);

    /// <summary>The clipboard's plain text, or <see langword="null"/> when it has none or cannot be read.</summary>
    string? ReadText();
}

public sealed class ClipboardWriteException : Exception
{
    public ClipboardWriteException()
        : base("Could not write to the clipboard. The queue has been paused.")
    {
    }

    public ClipboardWriteException(Exception innerException)
        : base("Could not write to the clipboard. The queue has been paused.", innerException)
    {
    }
}
