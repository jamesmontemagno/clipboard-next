using System.Text;
using System.Text.Json;
using ClipboardNext.Core;

namespace ClipboardNext.Core.Tests;

/// <summary>The macOS app's QueueTests, ported, plus the Windows clipboard-republish case.</summary>
public sealed class QueueTests : IDisposable
{
    private readonly QueueFixture _fixture = new();

    private DemoQueue Queue => _fixture.Queue;

    private FakeClipboard Clipboard => _fixture.Clipboard;

    private FakeScheduler Scheduler => _fixture.Scheduler;

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void PastesAdvanceAfterDelayThenFinishWithoutChangingClipboard()
    {
        Queue.Start();
        Assert.Equal("maui devflow list", Clipboard.Text);
        _fixture.Paste();
        Assert.Equal(0, Queue.Index);
        Scheduler.Fire();
        Assert.Equal(1, Queue.Index);
        Assert.Equal(1, Queue.DetectedPastes);
        Assert.NotNull(Queue.LastDetectedPaste);
        Assert.Equal("maui devflow ui query --text \"Sample movies and friends\"", Clipboard.Text);
        _fixture.Paste();
        Scheduler.Fire();
        var finalText = Clipboard.Text;
        var finalCount = Clipboard.ChangeCount;
        _fixture.Paste();
        Scheduler.Fire();
        Assert.Equal(QueueStatus.Finished, Queue.Status);
        Assert.Equal(3, Queue.DetectedPastes);
        Assert.Equal(finalText, Clipboard.Text);
        Assert.Equal(finalCount, Clipboard.ChangeCount);
        _fixture.Paste();
        Scheduler.Fire();
        Assert.Equal(3, Queue.DetectedPastes);
    }

    [Fact]
    public void BackRecopiesPreviousSnippetCancelsPendingAndReopensFinishedQueue()
    {
        Assert.False(Queue.CanGoBack);
        Queue.Start(Queue.Snippets[2].Id);
        _fixture.Paste();
        Scheduler.Fire();
        Assert.Equal(QueueStatus.Finished, Queue.Status);

        Queue.ManualPrevious();
        Assert.Equal(1, Queue.Index);
        Assert.Equal(QueueStatus.Running, Queue.Status);
        Assert.True(Queue.OwnsClipboard);
        Assert.Equal(Queue.Snippets[1].Text, Clipboard.Text);

        _fixture.Paste();
        Queue.ManualPrevious();
        Scheduler.Fire();
        Assert.Equal(0, Queue.Index);
        Assert.Equal("maui devflow list", Clipboard.Text);
        Assert.False(Queue.CanGoBack);
        Queue.ManualPrevious();
        Assert.Equal(0, Queue.Index);
    }

    [Fact]
    public void AdvanceDelayIsUsedClampedAndPersisted()
    {
        var settingsPath = Path.Combine(_fixture.Directory, "settings.json");
        var queue = _fixture.NewQueue(new FakeClipboard(), new AppSettings(settingsPath));
        Assert.Equal(DemoQueue.DefaultDelayMilliseconds, queue.AdvanceDelayMilliseconds);
        queue.SetAdvanceDelay(1200);
        queue.Start();
        _fixture.Paste(queue);
        Assert.Equal([1200], Scheduler.Delays);
        queue.SetAdvanceDelay(10);
        Assert.Equal(DemoQueue.MinimumDelayMilliseconds, queue.AdvanceDelayMilliseconds);
        queue.SetAdvanceDelay(99_999);
        var restored = _fixture.NewQueue(new FakeClipboard(), new AppSettings(settingsPath));
        Assert.Equal(DemoQueue.MaximumDelayMilliseconds, restored.AdvanceDelayMilliseconds);
    }

    [Fact]
    public void RepeatedAndRapidKeysOnlyAdvanceOnceAndOwnAppIsIgnored()
    {
        Queue.Start();
        Queue.KeyDown(isPasteGesture: false, isRepeat: false, isOwnApp: false);
        Queue.KeyUp();
        Queue.KeyDown(isPasteGesture: true, isRepeat: false, isOwnApp: true);
        Queue.KeyUp();
        Assert.Empty(Scheduler.Actions);
        Queue.KeyDown(isPasteGesture: true, isRepeat: false, isOwnApp: false);
        Queue.KeyDown(isPasteGesture: true, isRepeat: true, isOwnApp: false);
        Queue.KeyUp();
        _fixture.Paste();
        Assert.Single(Scheduler.Actions);
        Scheduler.Fire();
        Assert.Equal(1, Queue.Index);
        Assert.Equal(1, Queue.DetectedPastes);
    }

    [Fact]
    public void PauseAndRestartInvalidateDeferredPaste()
    {
        Queue.Start();
        _fixture.Paste();
        Queue.Pause("Keyboard monitoring stopped");
        Scheduler.Fire();
        Assert.Equal(0, Queue.Index);
        Assert.Equal(QueueStatus.Paused, Queue.Status);
        Queue.Resume();
        _fixture.Paste();
        Queue.Start(Queue.Snippets[2].Id);
        Scheduler.Fire();
        Assert.Equal(2, Queue.Index);
        Assert.Equal(0, Queue.DetectedPastes);
    }

    [Fact]
    public void ExternalCopyPausesAndPreservesContentEvenBeforeTimerPoll()
    {
        Queue.Start();
        _fixture.Paste();
        Clipboard.Write("Unrelated user content");
        Scheduler.Fire();
        Assert.Equal(QueueStatus.Paused, Queue.Status);
        Assert.False(Queue.OwnsClipboard);
        Assert.Equal(0, Queue.Index);
        Assert.Equal("Unrelated user content", Clipboard.Text);
        Assert.Equal(0, Queue.DetectedPastes);
        Queue.Resume();
        Assert.Equal("maui devflow list", Clipboard.Text);
        Clipboard.Write("Another copy");
        Queue.CheckClipboard();
        Assert.Equal(QueueStatus.Paused, Queue.Status);
        Assert.Equal("Another copy", Clipboard.Text);
    }

    [Fact]
    public void RepublishedSnippetTextKeepsTheQueueRunning()
    {
        Queue.Start();
        var notifications = 0;
        Queue.Changed += (_, _) => notifications++;

        // A clipboard sync tool rewrites the same text: the count moves, the content does not.
        Clipboard.ChangeCount += 2;
        Queue.CheckClipboard();
        Assert.Equal(QueueStatus.Running, Queue.Status);
        Assert.True(Queue.OwnsClipboard);
        Assert.Equal(0, notifications);

        _fixture.Paste();
        Scheduler.Fire();
        Assert.Equal(1, Queue.Index);

        // Polling an unchanged clipboard reads nothing and notifies no one.
        var reads = Clipboard.Reads;
        notifications = 0;
        Queue.CheckClipboard();
        Assert.Equal(reads, Clipboard.Reads);
        Assert.Equal(0, notifications);

        // Windows hands multiline text back with CR LF line endings; it is still the snippet.
        Queue.ManualNext();
        Clipboard.Text = "Inspect the running app.\r\nVerify the theme changed.";
        Clipboard.ChangeCount++;
        Queue.CheckClipboard();
        Assert.Equal(QueueStatus.Running, Queue.Status);
        Assert.True(Queue.OwnsClipboard);
    }

    [Fact]
    public void ManualSkipSelectedStartAndWriteFailure()
    {
        Queue.Start(Queue.Snippets[1].Id);
        Assert.Equal(1, Queue.Index);
        Queue.Pause();
        Queue.ManualNext();
        Assert.Equal(2, Queue.Index);
        Assert.Equal(QueueStatus.Paused, Queue.Status);
        Assert.Contains('\n', Clipboard.Text);
        Clipboard.Fails = true;
        Queue.Start();
        Assert.Equal(QueueStatus.Paused, Queue.Status);
        Assert.NotNull(Queue.Message);
        Assert.False(Queue.OwnsClipboard);
    }

    [Fact]
    public void RestoreNeverWritesClipboardOrArmsMonitoring()
    {
        Queue.Start();
        var restoredClipboard = new FakeClipboard();
        var restored = _fixture.NewQueue(restoredClipboard);
        Assert.Equal(Queue.Snippets, restored.Snippets);
        Assert.Equal(QueueStatus.Idle, restored.Status);
        Assert.Null(restored.Index);
        Assert.Equal(0, restoredClipboard.ChangeCount);
        Assert.Equal("Original clipboard", restoredClipboard.Text);
        Assert.Equal(1, Clipboard.ChangeCount);
    }

    [Fact]
    public void MalformedSavedQueueIsNotOverwritten()
    {
        var invalid = Encoding.UTF8.GetBytes("not JSON");
        File.WriteAllBytes(_fixture.StoragePath, invalid);
        var restored = _fixture.NewQueue(Clipboard);
        Assert.NotNull(restored.StorageError);
        restored.Update([new Snippet("echo hello")]);
        Assert.Equal(invalid, File.ReadAllBytes(_fixture.StoragePath));
        restored.RetrySave();
        Assert.Null(restored.StorageError);
        using var saved = JsonDocument.Parse(File.ReadAllBytes(_fixture.StoragePath));
        Assert.Equal("echo hello", saved.RootElement[0].GetProperty("text").GetString());
        Assert.Equal(
            new[] { "queue.json" },
            Directory.GetFiles(_fixture.Directory).Select(file => Path.GetFileName(file)).ToArray());
    }

    [Fact]
    public void FailedSaveIsReportedThenRetriedWithTheNextChange()
    {
        // A file where the queue's folder should be makes every save fail.
        var blocked = Path.Combine(_fixture.Directory, "blocked");
        File.WriteAllText(blocked, "in the way");
        var queue = new DemoQueue(Clipboard, Path.Combine(blocked, "queue.json"), new AppSettings(), Scheduler.Schedule);
        queue.Update([new Snippet("echo one")]);
        Assert.NotNull(queue.StorageError);
        Assert.Single(queue.Snippets);

        File.Delete(blocked);
        queue.Update([new Snippet("echo one"), new Snippet("echo two")]);
        Assert.Null(queue.StorageError);
        using var saved = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(blocked, "queue.json")));
        Assert.Equal(2, saved.RootElement.GetArrayLength());
    }

    [Fact]
    public void EditsRequirePauseAndInvalidatePendingWork()
    {
        Queue.Start();
        var original = Queue.Snippets.ToList();
        Queue.Update([]);
        Assert.Equal(original, Queue.Snippets);
        Assert.NotNull(Queue.Message);
        _fixture.Paste();
        Queue.Pause();
        List<Snippet> reordered = [original[2], original[1], original[0]];
        Queue.Update(reordered);
        Scheduler.Fire();
        Assert.Equal(2, Queue.Index);
        Assert.Equal(QueueStatus.Idle, Queue.Status);
        Assert.False(Queue.OwnsClipboard);
        Queue.Start(original[0].Id);
        Assert.Equal(2, Queue.Index);
    }

    [Fact]
    public void EmptySnippetCannotBeArmed()
    {
        Queue.Update([new Snippet(" \n ")]);
        Queue.Start();
        Assert.Equal(QueueStatus.Idle, Queue.Status);
        Assert.Equal(0, Clipboard.ChangeCount);
        Assert.NotNull(Queue.Message);
    }

    [Fact]
    public void QueueSavedByTheMacAppLoadsWithAndWithoutTitles()
    {
        File.WriteAllText(
            _fixture.StoragePath,
            """
            [{"id":"C6183472-34CD-478A-BBEB-C488FE257079","title":"Old title","text":"maui devflow list"},
             {"id":"0B0E4C5A-7F0B-4B55-9B0C-0C3B4A0E8A11","text":"maui devflow agent status"},
             {"id":"5d0f3a52-6f0e-4b53-8f0e-2d6b7f6a9c01","title":null,"text":"lowercase id, null title"}]
            """);
        var restored = _fixture.NewQueue(Clipboard);
        Assert.Null(restored.StorageError);
        Assert.Equal(
            new[] { "maui devflow list", "maui devflow agent status", "lowercase id, null title" },
            restored.Snippets.Select(snippet => snippet.Text).ToArray());
        Assert.Equal(new[] { "Old title", "", "" }, restored.Snippets.Select(snippet => snippet.Title).ToArray());
        Assert.Equal(Guid.Parse("C6183472-34CD-478A-BBEB-C488FE257079"), restored.Snippets[0].Id);

        // What is written back keeps the key names and order the macOS app reads.
        restored.Update(restored.Snippets.Take(1));
        using var saved = JsonDocument.Parse(File.ReadAllBytes(_fixture.StoragePath));
        Assert.Equal(
            new[] { "id", "title", "text" },
            saved.RootElement[0].EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal("c6183472-34cd-478a-bbeb-c488fe257079", saved.RootElement[0].GetProperty("id").GetString());
    }

    [Theory]
    [InlineData("""[{"id":"C6183472-34CD-478A-BBEB-C488FE257079"}]""")]
    [InlineData("""[{"text":"no id"}]""")]
    [InlineData("""[{"id":"C6183472-34CD-478A-BBEB-C488FE257079","text":null}]""")]
    [InlineData("[null]")]
    [InlineData("null")]
    public void SavedQueueMissingRequiredDataIsReportedNotLoaded(string json)
    {
        File.WriteAllText(_fixture.StoragePath, json);
        var restored = _fixture.NewQueue(Clipboard);
        Assert.NotNull(restored.StorageError);
        Assert.Empty(restored.Snippets);
    }

    [Fact]
    public void BadgeTitleFollowsImportedCurrentSnippetThroughTransportAndCompletion()
    {
        const string markdown = """
            # Seed demo data
            ```sh
            seed-demo
            ```
            # Show results
            ```sh
            show-results
            ```
            """;
        Queue.Update(MarkdownImport.Parse(markdown, wholeBlocks: false).Select(candidate => candidate.Snippet));
        Assert.Equal("", QueuePresentation.BadgeTitle(Queue));
        Assert.Equal("0/2", QueuePresentation.BadgeText(Queue));

        Queue.Start();
        Assert.Equal("Seed demo data", QueuePresentation.BadgeTitle(Queue));
        Assert.Equal("1/2", QueuePresentation.BadgeText(Queue));
        Assert.Equal("seed-demo", Clipboard.Text);
        _fixture.Paste();
        Assert.Equal("Seed demo data", QueuePresentation.BadgeTitle(Queue));
        Scheduler.Fire();
        Assert.Equal("Show results", QueuePresentation.BadgeTitle(Queue));
        Assert.Equal("2/2", QueuePresentation.BadgeText(Queue));
        Assert.Equal("show-results", Clipboard.Text);

        Queue.Pause();
        Assert.Equal("Show results", QueuePresentation.BadgeTitle(Queue));
        Queue.ManualPrevious();
        Assert.Equal("Seed demo data", QueuePresentation.BadgeTitle(Queue));
        Queue.Resume();
        Assert.Equal("Seed demo data", QueuePresentation.BadgeTitle(Queue));
        Queue.ManualNext();
        Queue.ManualNext();
        Assert.Equal(QueueStatus.Finished, Queue.Status);
        Assert.Equal("Show results", QueuePresentation.BadgeTitle(Queue));
        Assert.Equal("2/2", QueuePresentation.BadgeText(Queue));

        Queue.Start(Queue.Snippets[1].Id);
        Assert.Equal("Show results", QueuePresentation.BadgeTitle(Queue));
        Assert.Equal("2/2", QueuePresentation.BadgeText(Queue));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void BadgeWithoutTitleNeverFallsBackToCommandText(string title)
    {
        Queue.Update([new Snippet("command-not-for-the-badge", title)]);
        Queue.Start();
        Assert.Equal("", QueuePresentation.BadgeTitle(Queue));
        Assert.Equal("1/1", QueuePresentation.BadgeText(Queue));

        Queue.Pause();
        Queue.Update([]);
        Assert.Equal("", QueuePresentation.BadgeTitle(Queue));
        Assert.Equal("0/0", QueuePresentation.BadgeText(Queue));
    }

    [Fact]
    public void BadgeNormalizesTitleWhitespaceWithoutTruncatingOrChangingStoredContent()
    {
        var words = string.Join(" ", Enumerable.Repeat("Seed \u6570\u636e demo", 30));
        var title = $" \t{words}\r\n  show\tresults ";
        Queue.Update([new Snippet("seed-demo", title)]);
        Queue.Start();
        Assert.Equal($"{words} show results", QueuePresentation.BadgeTitle(Queue));
        Assert.Equal(title, Queue.Current?.Title);
        Assert.Equal(title, _fixture.NewQueue(new FakeClipboard()).Snippets[0].Title);
        Assert.Equal("seed-demo", Clipboard.Text);
    }

    [Fact]
    public void PresentationFollowsQueueState()
    {
        Assert.Equal("3 snippets", Queue.Progress);
        Assert.Equal("0/3", QueuePresentation.BadgeText(Queue));
        Assert.Equal(QueuePrimaryAction.Start, QueuePresentation.PrimaryAction(Queue));
        Assert.Equal("Clipboard Next · Ready", QueuePresentation.TrayToolTip(Queue));

        Queue.Start(Queue.Snippets[2].Id);
        Assert.Equal("3 of 3", Queue.Progress);
        Assert.Equal("3/3", QueuePresentation.BadgeText(Queue));
        Assert.Equal(QueuePrimaryAction.Pause, QueuePresentation.PrimaryAction(Queue));
        Assert.Equal("Clipboard Next · 3 of 3", QueuePresentation.TrayToolTip(Queue));
        Assert.True(QueuePresentation.CanGoNext(Queue));

        Queue.Pause();
        Assert.Equal(QueuePrimaryAction.Resume, QueuePresentation.PrimaryAction(Queue));

        Queue.ManualNext();
        Assert.Equal(QueueStatus.Finished, Queue.Status);
        Assert.Equal(QueuePrimaryAction.Start, QueuePresentation.PrimaryAction(Queue));
        Assert.False(QueuePresentation.CanGoNext(Queue));

        Queue.Update([new Snippet("only")]);
        Assert.Equal("1 snippet", Queue.Progress);
    }
}
