using ClipboardNext.Core;

namespace ClipboardNext.Core.Tests;

/// <summary>The macOS app's MarkdownImportTests, ported.</summary>
public sealed class MarkdownImportTests
{
    [Fact]
    public void TheaterLogCommandsAndOptionalPromptsInSourceOrder()
    {
        const string markdown = """
            # Connect
            **Say:** "Inspect it from a terminal."
            ```powershell
            maui devflow list
            maui devflow agent status

            maui devflow ui query --text "Sample movies and friends" --wait-until exists --timeout 5 --fields "id,type,text"
            ```
            ## Copilot
            > Connect to TheaterLog.
            > Verify the theme actually changed.

            ## Workaround
            ~~~bash
            DOTNET_SYSTEM_NET_DISABLEIPV6=1 maui devflow broker start --foreground
            ~~~
            """;
        var items = MarkdownImport.Parse(markdown, wholeBlocks: false);
        Assert.Equal(5, items.Count);
        Assert.Equal("maui devflow list", items[0].Snippet.Text);
        Assert.Contains("\"Sample movies and friends\"", items[2].Snippet.Text);
        Assert.Equal("Connect to TheaterLog.\nVerify the theme actually changed.", items[3].Snippet.Text);
        Assert.False(items[3].Selected);
        Assert.True(items[4].Selected);
        Assert.Equal("Workaround · bash", items[4].Context);
        Assert.Equal(
            new[] { "Connect", "Connect", "Connect", "Copilot", "Workaround" },
            items.Select(item => item.Snippet.Title).ToArray());
        Assert.Equal("", MarkdownImport.Parse("```\nls\n```", wholeBlocks: false)[0].Snippet.Title);
    }

    [Fact]
    public void WholeBlockPreservesWhitespaceQuotesAndInnerFences()
    {
        const string markdown = "# Script\r\n````bash\r\n  echo \"hello\" \\\r\n    --flag `date`\r\n\r\n```\r\n````\r\n";
        var items = MarkdownImport.Parse(markdown, wholeBlocks: true);
        Assert.Single(items);
        Assert.Equal("  echo \"hello\" \\\n    --flag `date`\n\n```", items[0].Snippet.Text);
        var split = MarkdownImport.Parse(markdown, wholeBlocks: false);
        Assert.Equal(
            new[] { "  echo \"hello\" \\", "    --flag `date`", "```" },
            split.Select(item => item.Snippet.Text).ToArray());
    }

    [Fact]
    public void MalformedFenceAndNoSnippetsHaveExplicitErrors()
    {
        var unclosed = Assert.Throws<MarkdownImportException>(
            () => MarkdownImport.Parse("# Broken\n```sh\necho hello", wholeBlocks: false));
        Assert.Contains("line 2", unclosed.Message);
        Assert.Throws<MarkdownImportException>(() => MarkdownImport.Parse("Just prose.", wholeBlocks: true));
        Assert.Throws<MarkdownImportException>(() => MarkdownImport.Parse("```\n \n```", wholeBlocks: false));
    }

    [Fact]
    public void ClosingFenceMustMatchMarkerAndHaveNoTrailingText()
    {
        var items = MarkdownImport.Parse("~~~sh\n```\n~~~not a close\n~~~", wholeBlocks: true);
        Assert.Equal("```\n~~~not a close", items[0].Snippet.Text);
    }

    [Fact]
    public void FilesMustBeUtf8AndMayStartWithAByteOrderMark()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ClipboardNextTests-{Guid.NewGuid():N}.md");
        try
        {
            File.WriteAllBytes(path, [0xEF, 0xBB, 0xBF, .. "```\necho héllo\n```"u8]);
            Assert.Equal("echo héllo", MarkdownImport.Parse(MarkdownImport.ReadFile(path), wholeBlocks: false)[0].Snippet.Text);

            // "é" in Windows-1252 is a lone 0xE9, which is not valid UTF-8.
            File.WriteAllBytes(path, [.. "```\necho h"u8, 0xE9, .. "llo\n```"u8]);
            var error = Assert.Throws<MarkdownImportException>(() => MarkdownImport.ReadFile(path));
            Assert.Contains("UTF-8", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
