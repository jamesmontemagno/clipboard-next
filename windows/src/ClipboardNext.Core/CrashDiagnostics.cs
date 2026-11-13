namespace ClipboardNext.Core;

/// <summary>
/// Best-effort persistent logger for unexpected exceptions in the packaged app, where
/// <c>Debug.WriteLine</c> is invisible. Writes to <c>Logs\crash.log</c> in the app's data folder.
/// The file is capped and rolled so it cannot grow without bound. It records exception details
/// only, never queue text or keystrokes. Never throws.
/// </summary>
public static class CrashDiagnostics
{
    private const string LogFileName = "crash.log";
    private const string PreviousLogFileName = "crash.previous.log";
    private const long MaxLogBytes = 512 * 1024;

    private static readonly object Gate = new();

    public static string LogPath => Path.Combine(AppPaths.LogsDirectory, LogFileName);

    /// <summary>Appends a timestamped entry describing an exception to the default crash log.</summary>
    public static void Log(string source, object? exception, bool handled) =>
        Log(LogPath, source, exception, handled);

    /// <summary>Appends a timestamped entry describing an exception to the given crash log.</summary>
    public static void Log(string logPath, string source, object? exception, bool handled)
    {
        lock (Gate)
        {
            try
            {
                var directory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                RollIfNeeded(logPath);

                var entry =
                    $"=== {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | {source} | {(handled ? "handled" : "unhandled")} | pid {Environment.ProcessId}{Environment.NewLine}" +
                    $"{exception ?? "(no exception object)"}{Environment.NewLine}{Environment.NewLine}";
                File.AppendAllText(logPath, entry);
            }
            catch
            {
                // Best-effort only.
            }
        }
    }

    private static void RollIfNeeded(string logPath)
    {
        var file = new FileInfo(logPath);
        if (!file.Exists || file.Length < MaxLogBytes)
        {
            return;
        }

        var directory = Path.GetDirectoryName(logPath) ?? string.Empty;
        File.Move(logPath, Path.Combine(directory, PreviousLogFileName), overwrite: true);
    }
}
