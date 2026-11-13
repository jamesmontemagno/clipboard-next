using System.Text.Json;

namespace ClipboardNext.Core;

/// <summary>
/// Runs an action after a delay and returns a callback that cancels it.
/// The action must run on the same thread that uses the queue.
/// </summary>
public delegate Action QueueScheduler(int milliseconds, Action action);

/// <summary>
/// The demo queue: an ordered list of snippets, the one currently on the clipboard, and the
/// rules for moving to the next one after a paste. A direct port of the macOS app's
/// <c>DemoQueue</c>. Not thread-safe; use it from one thread (the UI thread).
/// </summary>
public sealed class DemoQueue
{
    public const int MinimumDelayMilliseconds = 100;
    public const int MaximumDelayMilliseconds = 3000;
    public const int DefaultDelayMilliseconds = 350;

    private readonly IClipboardAccess _clipboard;
    private readonly string _storagePath;
    private readonly AppSettings _settings;
    private readonly QueueScheduler _schedule;
    private readonly TimeProvider _time;
    private List<Snippet> _snippets = [];
    private long? _ownedChangeCount;
    private Action? _cancelPending;
    private int _generation;
    private bool _pasteKeyDown;
    private bool _loadFailed;

    public DemoQueue(
        IClipboardAccess clipboard,
        string storagePath,
        AppSettings settings,
        QueueScheduler schedule,
        TimeProvider? time = null)
    {
        _clipboard = clipboard;
        _storagePath = storagePath;
        _settings = settings;
        _schedule = schedule;
        _time = time ?? TimeProvider.System;
        AdvanceDelayMilliseconds = ClampDelay(settings.AdvanceDelayMilliseconds ?? DefaultDelayMilliseconds);

        try
        {
            if (File.Exists(storagePath))
            {
                _snippets = QueueFile.Read(storagePath);
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _loadFailed = true;
            StorageError = $"Could not load your saved queue: {ex.Message} The saved file has not been replaced.";
        }
    }

    /// <summary>Raised after any state the UI shows has changed.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<Snippet> Snippets => _snippets;

    /// <summary>The position of the current snippet, or <see langword="null"/> before the queue has started.</summary>
    public int? Index { get; private set; }

    public QueueStatus Status { get; private set; }

    public string? Message { get; private set; }

    public string? StorageError { get; private set; }

    /// <summary>Whether the current snippet is still what the clipboard holds.</summary>
    public bool OwnsClipboard { get; private set; }

    public int DetectedPastes { get; private set; }

    public DateTimeOffset? LastDetectedPaste { get; private set; }

    /// <summary>Wait after the paste key is released before the next snippet replaces the clipboard.</summary>
    public int AdvanceDelayMilliseconds { get; private set; }

    public Snippet? Current => Index is { } index && index >= 0 && index < _snippets.Count ? _snippets[index] : null;

    public Snippet? Next => Index is { } index && index + 1 >= 0 && index + 1 < _snippets.Count ? _snippets[index + 1] : null;

    public string Progress => Index is { } index
        ? $"{index + 1} of {_snippets.Count}"
        : _snippets.Count == 1 ? "1 snippet" : $"{_snippets.Count} snippets";

    public bool CanGoBack => (Index ?? 0) > 0;

    public void Update(IEnumerable<Snippet> items)
    {
        if (Status == QueueStatus.Running)
        {
            Message = "Pause before editing the queue.";
            RaiseChanged();
            return;
        }

        var currentId = Current?.Id;
        CancelPending();
        _snippets = items.ToList();
        Index = currentId is { } id && _snippets.FindIndex(snippet => snippet.Id == id) is >= 0 and var found ? found : null;
        Status = QueueStatus.Idle;
        OwnsClipboard = false;
        SaveCore();
        RaiseChanged();
    }

    /// <summary>Saves the current queue over a saved file that could not be loaded.</summary>
    public void RetrySave()
    {
        _loadFailed = false;
        StorageError = null;
        SaveCore();
        RaiseChanged();
    }

    public void Start(Guid? selectedId = null)
    {
        StartCore(selectedId);
        RaiseChanged();
    }

    public void Pause(string? reason = null)
    {
        PauseCore(reason);
        RaiseChanged();
    }

    public void Resume()
    {
        if (Current is null || Status != QueueStatus.Paused)
        {
            StartCore(null);
        }
        else
        {
            CancelPending();
            Message = null;
            Status = QueueStatus.Running;
            CopyCurrent();
        }

        RaiseChanged();
    }

    /// <summary>Skips to and copies the next snippet without waiting for a paste.</summary>
    public void ManualNext()
    {
        if (Current is null)
        {
            StartCore(null);
        }
        else
        {
            CancelPending();
            Advance();
        }

        RaiseChanged();
    }

    /// <summary>Puts the previous snippet back on the clipboard.</summary>
    public void ManualPrevious()
    {
        if (Index is not { } index || index <= 0)
        {
            return;
        }

        CancelPending();
        Index = index - 1;
        // Stepping back from the end reopens the queue so the next paste advances again.
        if (Status == QueueStatus.Finished)
        {
            Status = QueueStatus.Running;
            Message = null;
        }

        CopyCurrent();
        RaiseChanged();
    }

    /// <summary>Pauses the queue when something else has replaced the snippet on the clipboard.</summary>
    public void CheckClipboard()
    {
        if (CheckClipboardCore())
        {
            RaiseChanged();
        }
    }

    /// <summary>Reports that the paste key went down.</summary>
    /// <param name="isPasteGesture">Whether the key, with the modifiers held, is a paste shortcut.</param>
    /// <param name="isRepeat">Whether this is an auto-repeat of a key that is already held.</param>
    /// <param name="isOwnApp">Whether Clipboard Next itself is the foreground app.</param>
    public void KeyDown(bool isPasteGesture, bool isRepeat, bool isOwnApp)
    {
        if (Status != QueueStatus.Running || !isPasteGesture || isRepeat || isOwnApp || _pasteKeyDown || _cancelPending is not null)
        {
            return;
        }

        var changed = CheckClipboardCore();
        if (Status == QueueStatus.Running)
        {
            _pasteKeyDown = true;
        }

        if (changed)
        {
            RaiseChanged();
        }
    }

    /// <summary>Reports that the paste key was released; the next snippet follows after the delay.</summary>
    public void KeyUp()
    {
        if (!_pasteKeyDown)
        {
            return;
        }

        _pasteKeyDown = false;
        var token = _generation;
        _cancelPending = _schedule(AdvanceDelayMilliseconds, () => CompletePaste(token));
    }

    public void SetAdvanceDelay(int milliseconds)
    {
        AdvanceDelayMilliseconds = ClampDelay(milliseconds);
        _settings.AdvanceDelayMilliseconds = AdvanceDelayMilliseconds;
        RaiseChanged();
    }

    private static int ClampDelay(int value) => Math.Clamp(value, MinimumDelayMilliseconds, MaximumDelayMilliseconds);

    // The Windows clipboard holds CR LF line endings while queue files keep LF.
    private static bool IsSameText(string? clipboardText, string snippetText) =>
        clipboardText is not null
        && clipboardText.ReplaceLineEndings("\n") == snippetText.ReplaceLineEndings("\n");

    private void StartCore(Guid? selectedId)
    {
        CancelPending();
        if (_snippets.Count == 0)
        {
            Message = "Add or import a snippet first.";
            return;
        }

        if (_snippets.Any(snippet => string.IsNullOrWhiteSpace(snippet.Text)))
        {
            Message = "Remove or fill in empty snippets before starting.";
            return;
        }

        Index = selectedId is { } id && _snippets.FindIndex(snippet => snippet.Id == id) is >= 0 and var found ? found : 0;
        Message = null;
        Status = QueueStatus.Running;
        CopyCurrent();
    }

    private void PauseCore(string? reason)
    {
        CancelPending();
        if (Status == QueueStatus.Running)
        {
            Status = QueueStatus.Paused;
        }

        if (reason is not null)
        {
            Message = reason;
        }
    }

    private bool CheckClipboardCore()
    {
        if (_ownedChangeCount is not { } owned)
        {
            return false;
        }

        var count = _clipboard.ChangeCount;
        if (count == owned)
        {
            return false;
        }

        // Clipboard sync tools (remote desktop, virtual machines, clipboard managers) can republish
        // the same text, which changes the count but not what a paste produces. Only different
        // content counts as another app taking the clipboard.
        if (OwnsClipboard && Current is { } current && IsSameText(_clipboard.ReadText(), current.Text))
        {
            _ownedChangeCount = count;
            return false;
        }

        var changed = OwnsClipboard || Status == QueueStatus.Running;
        _ownedChangeCount = count;
        OwnsClipboard = false;
        if (Status == QueueStatus.Running)
        {
            PauseCore("Another app changed the clipboard. Resume to copy the current snippet again.");
        }

        return changed;
    }

    private void CompletePaste(int token)
    {
        if (token != _generation || Status != QueueStatus.Running)
        {
            return;
        }

        _cancelPending = null;
        CheckClipboardCore();
        if (Status == QueueStatus.Running)
        {
            DetectedPastes++;
            LastDetectedPaste = _time.GetUtcNow();
            Advance();
        }

        RaiseChanged();
    }

    private void Advance()
    {
        if (Index is not { } index || index + 1 >= _snippets.Count)
        {
            Status = QueueStatus.Finished;
            Message = "All snippets completed. The final snippet is still on the clipboard.";
            return;
        }

        Index = index + 1;
        CopyCurrent();
    }

    private void CopyCurrent()
    {
        if (Current is not { } current)
        {
            return;
        }

        try
        {
            _clipboard.Write(current.Text);
            _ownedChangeCount = _clipboard.ChangeCount;
            OwnsClipboard = true;
        }
        catch (ClipboardWriteException ex)
        {
            OwnsClipboard = false;
            PauseCore(ex.Message);
        }
    }

    private void SaveCore()
    {
        // A failed load must not silently overwrite the original file. A failed save is different:
        // it is tried again with the next change, so a passing problem clears by itself.
        if (_loadFailed)
        {
            return;
        }

        try
        {
            QueueFile.Write(_storagePath, _snippets);
            StorageError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StorageError = $"Could not save your queue: {ex.Message}";
        }
    }

    private void CancelPending()
    {
        _generation++;
        _cancelPending?.Invoke();
        _cancelPending = null;
        _pasteKeyDown = false;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
