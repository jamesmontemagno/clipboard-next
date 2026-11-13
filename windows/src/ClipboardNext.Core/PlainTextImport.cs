namespace ClipboardNext.Core;

public sealed class PlainTextImportException : Exception
{
    public PlainTextImportException()
        : base("No nonblank lines were found. Enter one item per line.")
    {
    }
}

public static class PlainTextImport
{
    public static IReadOnlyList<ImportCandidate> Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var result = lines.Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => new ImportCandidate(selected: true, new Snippet(line), string.Empty))
            .ToArray();
        if (result.Length == 0)
        {
            throw new PlainTextImportException();
        }

        return result;
    }
}
