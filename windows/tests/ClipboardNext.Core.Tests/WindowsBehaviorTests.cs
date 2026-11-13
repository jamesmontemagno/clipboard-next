using ClipboardNext.Core;

namespace ClipboardNext.Core.Tests;

public sealed class WindowsBehaviorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ClipboardNextTests-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Theory]
    // key, ctrl, shift, alt, win, terminal shortcuts on => is paste
    [InlineData(PasteGesture.VirtualKeyV, true, false, false, false, false, true)]
    [InlineData(PasteGesture.VirtualKeyV, true, false, false, false, true, true)]
    [InlineData(PasteGesture.VirtualKeyV, false, false, false, false, true, false)]
    [InlineData(PasteGesture.VirtualKeyV, true, true, false, false, true, true)]
    [InlineData(PasteGesture.VirtualKeyV, true, true, false, false, false, false)]
    [InlineData(PasteGesture.VirtualKeyInsert, false, true, false, false, true, true)]
    [InlineData(PasteGesture.VirtualKeyInsert, false, true, false, false, false, false)]
    [InlineData(PasteGesture.VirtualKeyInsert, true, false, false, false, true, false)]
    [InlineData(PasteGesture.VirtualKeyInsert, false, false, false, false, true, false)]
    // Win+V is clipboard history, Ctrl+Alt+V is Paste Special, and AltGr arrives as Ctrl+Alt.
    [InlineData(PasteGesture.VirtualKeyV, false, false, false, true, true, false)]
    [InlineData(PasteGesture.VirtualKeyV, true, false, false, true, true, false)]
    [InlineData(PasteGesture.VirtualKeyV, true, false, true, false, true, false)]
    [InlineData(0x43, true, false, false, false, true, false)]
    public void OnlyPasteShortcutsCountAsPaste(
        int key, bool control, bool shift, bool alt, bool windows, bool terminalShortcuts, bool expected)
    {
        Assert.Equal(expected, PasteGesture.IsPaste(key, control, shift, alt, windows, terminalShortcuts));
    }

    [Fact]
    public void OnlyTheTwoPasteKeysAreEverExamined()
    {
        var triggers = Enumerable.Range(0, 256).Where(PasteGesture.IsTriggerKey).ToArray();
        Assert.Equal(new[] { PasteGesture.VirtualKeyInsert, PasteGesture.VirtualKeyV }, triggers);
    }

    [Fact]
    public void BadgeDefaultsToTopRightAndReturnsThereWhenItsDisplayIsGone()
    {
        var primary = new PixelRect(0, 0, 1920, 1040);
        var second = new PixelRect(1920, -200, 2560, 1400);
        const int width = 104, height = 32, margin = 20;

        Assert.Equal((1796, 20), BadgePlacement.Resolve(null, width, height, margin, primary, [primary, second]));

        // A spot on the second display is kept while that display is connected.
        Assert.Equal((3000, 500), BadgePlacement.Resolve((3000, 500), width, height, margin, primary, [primary, second]));
        Assert.Equal((1796, 20), BadgePlacement.Resolve((3000, 500), width, height, margin, primary, [primary]));

        // Partly visible still counts; only a badge with no pixel on any display is moved back.
        Assert.Equal((-100, 10), BadgePlacement.Resolve((-100, 10), width, height, margin, primary, [primary]));
        Assert.Equal((-104, 10), BadgePlacement.Resolve((-104, 10), width, height, margin, primary, [second, primary with { X = -104 }]));
        Assert.Equal((1796, 20), BadgePlacement.Resolve((-104, 10), width, height, margin, primary, [primary]));
    }

    [Fact]
    public void SettingsPersistAndDefaultToManualModeWithPlainPasteOnly()
    {
        var path = Path.Combine(_directory, "settings.json");
        var settings = new AppSettings(path);
        Assert.False(settings.PasteDetectionEnabled);
        Assert.False(settings.DetectTerminalPasteShortcuts);
        Assert.False(settings.ShowFloatingBadge);
        Assert.Null(settings.BadgePosition);
        Assert.Null(settings.AdvanceDelayMilliseconds);
        Assert.Equal(ImportFormat.Markdown, settings.ImportFormat);

        settings.PasteDetectionEnabled = true;
        settings.DetectTerminalPasteShortcuts = true;
        settings.ShowFloatingBadge = true;
        settings.BadgePosition = (-1200, 48);
        settings.ImportFormat = ImportFormat.PlainText;

        var restored = new AppSettings(path);
        Assert.True(restored.PasteDetectionEnabled);
        Assert.True(restored.DetectTerminalPasteShortcuts);
        Assert.True(restored.ShowFloatingBadge);
        Assert.Equal((-1200, 48), restored.BadgePosition);
        Assert.Equal(ImportFormat.PlainText, restored.ImportFormat);

        restored.BadgePosition = null;
        restored.ImportFormat = ImportFormat.Markdown;
        Assert.Null(new AppSettings(path).BadgePosition);
        Assert.Equal(ImportFormat.Markdown, new AppSettings(path).ImportFormat);
    }

    [Fact]
    public void SettingsWithoutImportFormatKeepExistingPreferencesAndDefaultToMarkdown()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, """{"showFloatingBadge":true,"detectTerminalPasteShortcuts":true}""");

        var settings = new AppSettings(path);
        Assert.Equal(ImportFormat.Markdown, settings.ImportFormat);
        Assert.True(settings.ShowFloatingBadge);
        Assert.True(settings.DetectTerminalPasteShortcuts);

        settings.ImportFormat = ImportFormat.PlainText;
        var restored = new AppSettings(path);
        Assert.Equal(ImportFormat.PlainText, restored.ImportFormat);
        Assert.True(restored.ShowFloatingBadge);
        Assert.True(restored.DetectTerminalPasteShortcuts);
    }

    [Fact]
    public void UnreadableSettingsFallBackToDefaults()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{ not json");
        var settings = new AppSettings(path);
        Assert.False(settings.PasteDetectionEnabled);
        Assert.False(settings.ShowFloatingBadge);
    }
}
