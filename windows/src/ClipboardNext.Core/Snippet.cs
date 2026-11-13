namespace ClipboardNext.Core;

/// <summary>One queued command or prompt. The optional title is a label and is never copied.</summary>
public sealed record Snippet(Guid Id, string Text, string Title = "")
{
    public Snippet(string text, string title = "")
        : this(Guid.NewGuid(), text, title)
    {
    }
}
