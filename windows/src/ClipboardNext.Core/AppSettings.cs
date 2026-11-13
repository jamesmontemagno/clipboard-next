using System.Text.Json;

namespace ClipboardNext.Core;

internal sealed class AppSettingsData
{
    public int? AdvanceDelayMilliseconds { get; set; }

    public bool ShowFloatingBadge { get; set; }

    public int? BadgeX { get; set; }

    public int? BadgeY { get; set; }

    public bool PasteDetectionEnabled { get; set; }

    public bool DetectTerminalPasteShortcuts { get; set; }
}

/// <summary>
/// The app's small set of remembered preferences, stored as JSON next to the queue.
/// Every change is saved immediately. Preferences are not critical: an unreadable file
/// falls back to defaults and a failed save is ignored, so neither can stop a demo.
/// </summary>
public sealed class AppSettings
{
    private readonly string? _path;
    private readonly AppSettingsData _data;

    /// <param name="path">The settings file, or <see langword="null"/> to keep settings in memory only.</param>
    public AppSettings(string? path = null)
    {
        _path = path;
        _data = Load(path);
    }

    /// <summary>The saved wait between a detected paste and the next snippet, before clamping.</summary>
    public int? AdvanceDelayMilliseconds
    {
        get => _data.AdvanceDelayMilliseconds;
        set
        {
            _data.AdvanceDelayMilliseconds = value;
            Save();
        }
    }

    public bool ShowFloatingBadge
    {
        get => _data.ShowFloatingBadge;
        set
        {
            _data.ShowFloatingBadge = value;
            Save();
        }
    }

    /// <summary>The badge's top-left corner in physical screen pixels, once the user has moved it.</summary>
    public (int X, int Y)? BadgePosition
    {
        get => _data is { BadgeX: { } x, BadgeY: { } y } ? (x, y) : null;
        set
        {
            _data.BadgeX = value?.X;
            _data.BadgeY = value?.Y;
            Save();
        }
    }

    /// <summary>Whether the user has turned on automatic paste detection. Off until they ask for it.</summary>
    public bool PasteDetectionEnabled
    {
        get => _data.PasteDetectionEnabled;
        set
        {
            _data.PasteDetectionEnabled = value;
            Save();
        }
    }

    /// <summary>
    /// Whether Ctrl+Shift+V and Shift+Insert also count as a paste. Off by default, like the
    /// macOS app's plain Command-V: Ctrl+Shift+V is not a paste everywhere.
    /// </summary>
    public bool DetectTerminalPasteShortcuts
    {
        get => _data.DetectTerminalPasteShortcuts;
        set
        {
            _data.DetectTerminalPasteShortcuts = value;
            Save();
        }
    }

    private static AppSettingsData Load(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return new AppSettingsData();
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllBytes(path), CoreJsonContext.Default.AppSettingsData)
                ?? new AppSettingsData();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettingsData();
        }
    }

    private void Save()
    {
        if (_path is null)
        {
            return;
        }

        try
        {
            AtomicFile.WriteAllBytes(_path, JsonSerializer.SerializeToUtf8Bytes(_data, CoreJsonContext.Default.AppSettingsData));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
