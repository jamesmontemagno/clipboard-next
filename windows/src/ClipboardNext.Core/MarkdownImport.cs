using System.Globalization;
using System.Text;

namespace ClipboardNext.Core;

/// <summary>A snippet found in Markdown presenter notes, offered for import.</summary>
public sealed class ImportCandidate
{
    public ImportCandidate(bool selected, Snippet snippet, string context)
    {
        Selected = selected;
        Snippet = snippet;
        Context = context;
    }

    public bool Selected { get; set; }

    public Snippet Snippet { get; }

    /// <summary>The heading (and language or "Prompt") the snippet was found under. Never copied.</summary>
    public string Context { get; }
}

public sealed class MarkdownImportException : Exception
{
    private MarkdownImportException(string message)
        : base(message)
    {
    }

    public static MarkdownImportException UnclosedFence(int line) =>
        new($"Unclosed code fence at line {line}. Close it before importing.");

    public static MarkdownImportException NoContent() =>
        new("No code blocks or blockquoted prompts were found.");

    public static MarkdownImportException NotUtf8() =>
        new("The file is not UTF-8 text.");
}

/// <summary>
/// Finds snippets in Markdown presenter notes: fenced code blocks (selected by default) and
/// blockquoted prompts (not selected). A direct port of the macOS app's <c>MarkdownImport</c>.
/// </summary>
public static class MarkdownImport
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Reads presenter notes from a UTF-8 file (with or without a byte-order mark).
    /// Throws <see cref="MarkdownImportException"/> for anything that is not valid UTF-8,
    /// rather than importing garbled commands.
    /// </summary>
    public static string ReadFile(string path)
    {
        try
        {
            return File.ReadAllText(path, StrictUtf8);
        }
        catch (DecoderFallbackException)
        {
            throw MarkdownImportException.NotUtf8();
        }
    }

    public static IReadOnlyList<ImportCandidate> Parse(string markdown, bool wholeBlocks)
    {
        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var result = new List<ImportCandidate>();
        var heading = "Untitled section";
        var headingFound = false;
        var i = 0;
        while (i < lines.Length)
        {
            var trimmed = TrimInline(lines[i]);
            if (Fence(trimmed) is { } opening)
            {
                var startLine = i + 1;
                var language = TrimInline(trimmed[opening.Count..]);
                i++;
                var content = new List<string>();
                var closed = false;
                while (i < lines.Length)
                {
                    var end = TrimInline(lines[i]);
                    if (Fence(end) is { } closing && closing.Character == opening.Character
                        && closing.Count >= opening.Count
                        && TrimInline(end[closing.Count..]).Length == 0)
                    {
                        closed = true;
                        break;
                    }

                    content.Add(lines[i]);
                    i++;
                }

                if (!closed)
                {
                    throw MarkdownImportException.UnclosedFence(startLine);
                }

                IEnumerable<string> texts = wholeBlocks ? [string.Join('\n', content)] : content;
                foreach (var text in texts)
                {
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        result.Add(new ImportCandidate(
                            selected: true,
                            new Snippet(text, headingFound ? heading : ""),
                            language.Length == 0 ? heading : $"{heading} · {language}"));
                    }
                }
            }
            else if (trimmed.StartsWith('>'))
            {
                var content = new List<string>();
                while (i < lines.Length)
                {
                    var line = TrimInline(lines[i]);
                    if (!line.StartsWith('>'))
                    {
                        break;
                    }

                    var text = line[1..];
                    content.Add(text.StartsWith(' ') ? text[1..] : text);
                    i++;
                }

                var prompt = string.Join('\n', content);
                if (!string.IsNullOrWhiteSpace(prompt))
                {
                    result.Add(new ImportCandidate(
                        selected: false,
                        new Snippet(prompt, headingFound ? heading : ""),
                        $"{heading} · Prompt"));
                }

                continue;
            }
            else if (trimmed.StartsWith('#'))
            {
                var title = trimmed.TrimStart('#');
                if (title.StartsWith(' '))
                {
                    heading = TrimInline(title);
                    headingFound = true;
                }
            }

            i++;
        }

        if (result.Count == 0)
        {
            throw MarkdownImportException.NoContent();
        }

        return result;
    }

    private static (char Character, int Count)? Fence(string line)
    {
        if (line.Length == 0 || line[0] is not ('`' or '~'))
        {
            return null;
        }

        var count = 0;
        while (count < line.Length && line[count] == line[0])
        {
            count++;
        }

        return count >= 3 ? (line[0], count) : null;
    }

    // Spaces and tabs only: a line's newlines are already gone, and other characters are content.
    private static string TrimInline(string value)
    {
        var start = 0;
        var end = value.Length;
        while (start < end && IsInlineSpace(value[start]))
        {
            start++;
        }

        while (end > start && IsInlineSpace(value[end - 1]))
        {
            end--;
        }

        return value[start..end];
    }

    private static bool IsInlineSpace(char c) =>
        c == '\t' || char.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator;
}
