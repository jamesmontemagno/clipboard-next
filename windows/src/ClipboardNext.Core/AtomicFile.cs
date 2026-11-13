namespace ClipboardNext.Core;

internal static class AtomicFile
{
    private const int ReplaceAttempts = 4;

    /// <summary>
    /// Writes a file by writing a sibling temporary file and renaming it over the destination,
    /// so a crash or power loss never leaves a half-written file behind.
    /// </summary>
    public static void WriteAllBytes(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            Replace(temporary, path);
        }
        catch
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            throw;
        }
    }

    // On Windows an antivirus scanner or the search indexer can hold the destination open for a
    // moment, which makes the rename fail. That passes within milliseconds, so try again briefly.
    private static void Replace(string temporary, string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(temporary, path, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < ReplaceAttempts)
            {
                Thread.Sleep(15 * attempt);
            }
        }
    }
}
