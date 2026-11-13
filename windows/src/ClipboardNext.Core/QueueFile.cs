using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClipboardNext.Core;

/// <summary>
/// Reads and writes <c>queue.json</c>. The shape matches the macOS app's file (<c>id</c>,
/// optional <c>title</c>, <c>text</c>), so a queue can be copied between platforms.
/// </summary>
internal static class QueueFile
{
    public static List<Snippet> Read(string path)
    {
        var records = JsonSerializer.Deserialize(File.ReadAllBytes(path), CoreJsonContext.Default.ListSnippetRecord)
            ?? throw new JsonException("The file does not contain a list of snippets.");
        return records
            .Select(record => record is { Id: { } id, Text: { } text }
                ? new Snippet(id, text, record.Title ?? "")
                : throw new JsonException("A snippet is missing its id or text."))
            .ToList();
    }

    public static void Write(string path, IEnumerable<Snippet> snippets)
    {
        var records = snippets
            .Select(snippet => new SnippetRecord { Id = snippet.Id, Title = snippet.Title, Text = snippet.Text })
            .ToList();
        AtomicFile.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(records, CoreJsonContext.Default.ListSnippetRecord));
    }
}

internal sealed class SnippetRecord
{
    [JsonPropertyName("id")]
    public Guid? Id { get; set; }

    // Queues saved before titles existed have no "title" key.
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

[JsonSerializable(typeof(List<SnippetRecord>))]
[JsonSerializable(typeof(AppSettingsData))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class CoreJsonContext : JsonSerializerContext;
