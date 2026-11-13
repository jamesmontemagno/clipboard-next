import XCTest
@testable import ClipboardNext

final class MarkdownImportTests: XCTestCase {
    func testTheaterLogCommandsAndOptionalPromptsInSourceOrder() throws {
        let markdown = """
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
        """
        let items = try MarkdownImport.parse(markdown, wholeBlocks: false)
        XCTAssertEqual(items.count, 5)
        XCTAssertEqual(items[0].snippet.text, "maui devflow list")
        XCTAssertTrue(items[2].snippet.text.contains("\"Sample movies and friends\""))
        XCTAssertEqual(items[3].snippet.text, "Connect to TheaterLog.\nVerify the theme actually changed.")
        XCTAssertFalse(items[3].selected)
        XCTAssertTrue(items[4].selected)
        XCTAssertEqual(items[4].context, "Workaround · bash")
        XCTAssertEqual(items.map(\.snippet.title), ["Connect", "Connect", "Connect", "Copilot", "Workaround"])
        XCTAssertEqual(try MarkdownImport.parse("```\nls\n```", wholeBlocks: false).first?.snippet.title, "")
    }

    func testWholeBlockPreservesWhitespaceQuotesAndInnerFences() throws {
        let markdown = "# Script\r\n````bash\r\n  echo \"hello\" \\\r\n    --flag `date`\r\n\r\n```\r\n````\r\n"
        let items = try MarkdownImport.parse(markdown, wholeBlocks: true)
        XCTAssertEqual(items.count, 1)
        XCTAssertEqual(items[0].snippet.text, "  echo \"hello\" \\\n    --flag `date`\n\n```")
        let split = try MarkdownImport.parse(markdown, wholeBlocks: false)
        XCTAssertEqual(split.map(\.snippet.text), ["  echo \"hello\" \\", "    --flag `date`", "```"])
    }

    func testMalformedFenceAndNoSnippetsHaveExplicitErrors() {
        XCTAssertThrowsError(try MarkdownImport.parse("# Broken\n```sh\necho hello", wholeBlocks: false)) { error in
            XCTAssertTrue(error.localizedDescription.contains("line 2"))
        }
        XCTAssertThrowsError(try MarkdownImport.parse("Just prose.", wholeBlocks: true))
        XCTAssertThrowsError(try MarkdownImport.parse("```\n \n```", wholeBlocks: false))
    }

    func testClosingFenceMustMatchMarkerAndHaveNoTrailingText() throws {
        let items = try MarkdownImport.parse("~~~sh\n```\n~~~not a close\n~~~", wholeBlocks: true)
        XCTAssertEqual(items.first?.snippet.text, "```\n~~~not a close")
    }

    func testPlainTextPreservesLinesWhitespaceAndDuplicatesWithoutTitles() throws {
        for separator in ["\n", "\r\n", "\r"] {
            let source = ["", "  echo \"hello\" \t", " \t ", "echo repeat", "echo repeat", ""]
                .joined(separator: separator)
            let items = try PlainTextImport.parse(source)
            XCTAssertEqual(items.map(\.snippet.text), ["  echo \"hello\" \t", "echo repeat", "echo repeat"])
            XCTAssertTrue(items.allSatisfy { $0.selected && $0.snippet.title.isEmpty && $0.context.isEmpty })
            XCTAssertEqual(Set(items.map(\.snippet.id)).count, 3)
        }
        let mixed = try PlainTextImport.parse("one\r\ntwo\rthree\nfour")
        XCTAssertEqual(mixed.map(\.snippet.text), ["one", "two", "three", "four"])
    }

    func testPlainTextTreatsMarkdownSyntaxLiterally() throws {
        let items = try PlainTextImport.parse("# Heading\n> prompt\n```sh\necho hello")
        XCTAssertEqual(items.map(\.snippet.text), ["# Heading", "> prompt", "```sh", "echo hello"])
        XCTAssertTrue(items.allSatisfy { $0.selected && $0.snippet.title.isEmpty })
    }

    func testPlainTextRejectsEmptyOrWhitespaceOnlyInputWithSpecificError() {
        for source in ["", " \t", "\r\n \t\r\n"] {
            XCTAssertThrowsError(try PlainTextImport.parse(source)) { error in
                XCTAssertTrue(error is PlainTextImportError)
                XCTAssertTrue(error.localizedDescription.contains("nonblank lines"))
            }
        }
    }
}
