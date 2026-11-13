using ClipboardNext.Core;

namespace ClipboardNext.Core.Tests;

internal sealed class FakeClipboard : IClipboardAccess
{
    public long ChangeCount { get; set; }

    public string Text { get; set; } = "Original clipboard";

    public bool Fails { get; set; }

    public int Reads { get; private set; }

    public void Write(string text)
    {
        if (Fails)
        {
            throw new ClipboardWriteException();
        }

        ChangeCount++;
        Text = text;
    }

    public string? ReadText()
    {
        Reads++;
        return Text;
    }
}

internal sealed class FakeScheduler
{
    public List<Action> Actions { get; private set; } = [];

    public List<int> Delays { get; } = [];

    public Action Schedule(int milliseconds, Action action)
    {
        Delays.Add(milliseconds);
        Actions.Add(action);
        // Intentionally deliver canceled work too, exercising the queue's generation guard.
        return () => { };
    }

    public void Fire()
    {
        var pending = Actions;
        Actions = [];
        pending.ForEach(action => action());
    }
}

/// <summary>A queue of three snippets saved under a temporary folder that is removed afterwards.</summary>
internal sealed class QueueFixture : IDisposable
{
    public QueueFixture()
    {
        Directory = Path.Combine(Path.GetTempPath(), $"ClipboardNextTests-{Guid.NewGuid():N}");
        StoragePath = Path.Combine(Directory, "queue.json");
        Queue = NewQueue(Clipboard);
        Queue.Update(
        [
            new Snippet("maui devflow list"),
            new Snippet("maui devflow ui query --text \"Sample movies and friends\""),
            new Snippet("Inspect the running app.\nVerify the theme changed."),
        ]);
    }

    public string Directory { get; }

    public string StoragePath { get; }

    public FakeClipboard Clipboard { get; } = new();

    public FakeScheduler Scheduler { get; } = new();

    public DemoQueue Queue { get; }

    public DemoQueue NewQueue(FakeClipboard clipboard, AppSettings? settings = null) =>
        new(clipboard, StoragePath, settings ?? new AppSettings(), Scheduler.Schedule);

    public void Paste(DemoQueue? queue = null)
    {
        queue ??= Queue;
        queue.KeyDown(isPasteGesture: true, isRepeat: false, isOwnApp: false);
        queue.KeyUp();
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Directory))
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
