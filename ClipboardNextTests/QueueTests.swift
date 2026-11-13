import XCTest
@testable import ClipboardNext

@MainActor
final class FakeClipboard: ClipboardAccess {
    var changeCount = 0
    var text = "Original clipboard"
    var fails = false
    func write(_ text: String) throws {
        if fails { throw ClipboardError.writeFailed }
        changeCount += 1
        self.text = text
    }
}

@MainActor
final class FakeScheduler {
    var actions: [@MainActor () -> Void] = []
    var delays: [Int] = []
    func schedule(_ milliseconds: Int, _ action: @escaping @MainActor () -> Void) -> (() -> Void) {
        delays.append(milliseconds)
        actions.append(action)
        // Intentionally deliver canceled work too, exercising the queue's generation guard.
        return {}
    }
    func fire() {
        let pending = actions
        actions = []
        pending.forEach { $0() }
    }
}

final class QueueTests: XCTestCase {
    @MainActor
    private func fixture() -> (DemoQueue, FakeClipboard, FakeScheduler, URL) {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("ClipboardNextTests-\(UUID().uuidString)", isDirectory: true)
            .appendingPathComponent("queue.json")
        let clipboard = FakeClipboard()
        let scheduler = FakeScheduler()
        let queue = DemoQueue(clipboard: clipboard, storageURL: url, schedule: scheduler.schedule)
        queue.update([
            Snippet(text: "maui devflow list"),
            Snippet(text: "maui devflow ui query --text \"Sample movies and friends\""),
            Snippet(text: "Inspect the running app.\nVerify the theme changed.")
        ])
        addTeardownBlock { try FileManager.default.removeItem(at: url.deletingLastPathComponent()) }
        return (queue, clipboard, scheduler, url)
    }

    @MainActor
    private func paste(_ queue: DemoQueue) {
        queue.keyDown(command: true, isRepeat: false, isOwnApp: false)
        queue.keyUp()
    }

    @MainActor
    func testPastesAdvanceAfterDelayThenFinishWithoutChangingClipboard() async {
        let (queue, clipboard, scheduler, _) = fixture()
        queue.start()
        XCTAssertEqual(clipboard.text, "maui devflow list")
        paste(queue)
        XCTAssertEqual(queue.index, 0)
        scheduler.fire()
        XCTAssertEqual(queue.index, 1)
        XCTAssertEqual(queue.detectedPastes, 1)
        XCTAssertEqual(clipboard.text, "maui devflow ui query --text \"Sample movies and friends\"")
        paste(queue)
        scheduler.fire()
        let finalText = clipboard.text
        let finalCount = clipboard.changeCount
        paste(queue)
        scheduler.fire()
        XCTAssertEqual(queue.status, .finished)
        XCTAssertEqual(queue.detectedPastes, 3)
        XCTAssertEqual(clipboard.text, finalText)
        XCTAssertEqual(clipboard.changeCount, finalCount)
        paste(queue)
        scheduler.fire()
        XCTAssertEqual(queue.detectedPastes, 3)
    }

    @MainActor
    func testBackRecopiesPreviousSnippetCancelsPendingAndReopensFinishedQueue() async {
        let (queue, clipboard, scheduler, _) = fixture()
        XCTAssertFalse(queue.canGoBack)
        queue.start(at: queue.snippets[2].id)
        paste(queue)
        scheduler.fire()
        XCTAssertEqual(queue.status, .finished)

        queue.manualPrevious()
        XCTAssertEqual(queue.index, 1)
        XCTAssertEqual(queue.status, .running)
        XCTAssertTrue(queue.ownsClipboard)
        XCTAssertEqual(clipboard.text, queue.snippets[1].text)

        paste(queue)
        queue.manualPrevious()
        scheduler.fire()
        XCTAssertEqual(queue.index, 0)
        XCTAssertEqual(clipboard.text, "maui devflow list")
        XCTAssertFalse(queue.canGoBack)
        queue.manualPrevious()
        XCTAssertEqual(queue.index, 0)
    }

    @MainActor
    func testAdvanceDelayIsUsedClampedAndPersisted() async {
        let suite = "ClipboardNextTests-\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suite)!
        addTeardownBlock { defaults.removePersistentDomain(forName: suite) }
        let url = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let scheduler = FakeScheduler()
        let queue = DemoQueue(clipboard: FakeClipboard(), storageURL: url, defaults: defaults, schedule: scheduler.schedule)
        XCTAssertEqual(queue.advanceDelayMilliseconds, DemoQueue.defaultDelay)
        queue.update([Snippet(text: "one"), Snippet(text: "two")])
        queue.setAdvanceDelay(milliseconds: 1200)
        queue.start()
        paste(queue)
        XCTAssertEqual(scheduler.delays, [1200])
        queue.setAdvanceDelay(milliseconds: 10)
        XCTAssertEqual(queue.advanceDelayMilliseconds, DemoQueue.delayRange.lowerBound)
        queue.setAdvanceDelay(milliseconds: 99_999)
        let restored = DemoQueue(clipboard: FakeClipboard(), storageURL: url, defaults: defaults, schedule: scheduler.schedule)
        XCTAssertEqual(restored.advanceDelayMilliseconds, DemoQueue.delayRange.upperBound)
    }

    @MainActor
    func testRepeatedAndRapidKeysOnlyAdvanceOnceAndOwnAppIsIgnored() async {
        let (queue, _, scheduler, _) = fixture()
        queue.start()
        queue.keyDown(command: false, isRepeat: false, isOwnApp: false)
        queue.keyUp()
        queue.keyDown(command: true, isRepeat: false, isOwnApp: true)
        queue.keyUp()
        XCTAssertTrue(scheduler.actions.isEmpty)
        queue.keyDown(command: true, isRepeat: false, isOwnApp: false)
        queue.keyDown(command: true, isRepeat: true, isOwnApp: false)
        queue.keyUp()
        paste(queue)
        XCTAssertEqual(scheduler.actions.count, 1)
        scheduler.fire()
        XCTAssertEqual(queue.index, 1)
        XCTAssertEqual(queue.detectedPastes, 1)
    }

    @MainActor
    func testPauseAndRestartInvalidateDeferredPaste() async {
        let (queue, _, scheduler, _) = fixture()
        queue.start()
        paste(queue)
        queue.pause("Monitor permission lost")
        scheduler.fire()
        XCTAssertEqual(queue.index, 0)
        XCTAssertEqual(queue.status, .paused)
        queue.resume()
        paste(queue)
        queue.start(at: queue.snippets[2].id)
        scheduler.fire()
        XCTAssertEqual(queue.index, 2)
        XCTAssertEqual(queue.detectedPastes, 0)
    }

    @MainActor
    func testExternalCopyPausesAndPreservesContentEvenBeforeTimerPoll() async throws {
        let (queue, clipboard, scheduler, _) = fixture()
        queue.start()
        paste(queue)
        try clipboard.write("Unrelated user content")
        scheduler.fire()
        XCTAssertEqual(queue.status, .paused)
        XCTAssertFalse(queue.ownsClipboard)
        XCTAssertEqual(queue.index, 0)
        XCTAssertEqual(clipboard.text, "Unrelated user content")
        XCTAssertEqual(queue.detectedPastes, 0)
        queue.resume()
        XCTAssertEqual(clipboard.text, "maui devflow list")
        try clipboard.write("Another copy")
        queue.checkClipboard()
        XCTAssertEqual(queue.status, .paused)
        XCTAssertEqual(clipboard.text, "Another copy")
    }

    @MainActor
    func testManualSkipSelectedStartAndWriteFailure() async {
        let (queue, clipboard, _, _) = fixture()
        queue.start(at: queue.snippets[1].id)
        XCTAssertEqual(queue.index, 1)
        queue.pause()
        queue.manualNext()
        XCTAssertEqual(queue.index, 2)
        XCTAssertEqual(queue.status, .paused)
        XCTAssertTrue(clipboard.text.contains("\n"))
        clipboard.fails = true
        queue.start()
        XCTAssertEqual(queue.status, .paused)
        XCTAssertNotNil(queue.message)
        XCTAssertFalse(queue.ownsClipboard)
    }

    @MainActor
    func testRestoreNeverWritesClipboardOrArmsMonitoring() async {
        let (queue, clipboard, scheduler, url) = fixture()
        queue.start()
        let restoredClipboard = FakeClipboard()
        let restored = DemoQueue(clipboard: restoredClipboard, storageURL: url, schedule: scheduler.schedule)
        XCTAssertEqual(restored.snippets, queue.snippets)
        XCTAssertEqual(restored.status, .idle)
        XCTAssertNil(restored.index)
        XCTAssertEqual(restoredClipboard.changeCount, 0)
        XCTAssertEqual(restoredClipboard.text, "Original clipboard")
        XCTAssertEqual(clipboard.changeCount, 1)
    }

    @MainActor
    func testMalformedSavedQueueIsNotOverwritten() async throws {
        let (_, clipboard, scheduler, url) = fixture()
        let invalid = Data("not JSON".utf8)
        try invalid.write(to: url)
        let restored = DemoQueue(clipboard: clipboard, storageURL: url, schedule: scheduler.schedule)
        XCTAssertNotNil(restored.storageError)
        restored.update([Snippet(text: "echo hello")])
        XCTAssertEqual(try Data(contentsOf: url), invalid)
        restored.retrySave()
        XCTAssertNil(restored.storageError)
        XCTAssertEqual(try JSONDecoder().decode([Snippet].self, from: Data(contentsOf: url)).first?.text, "echo hello")
    }

    @MainActor
    func testEditsRequirePauseAndInvalidatePendingWork() async {
        let (queue, _, scheduler, _) = fixture()
        queue.start()
        let original = queue.snippets
        queue.update([])
        XCTAssertEqual(queue.snippets, original)
        paste(queue)
        queue.pause()
        var reordered = original
        reordered.swapAt(0, 2)
        queue.update(reordered)
        scheduler.fire()
        XCTAssertEqual(queue.index, 2)
        XCTAssertEqual(queue.status, .idle)
        XCTAssertFalse(queue.ownsClipboard)
        queue.start(at: original[0].id)
        XCTAssertEqual(queue.index, 2)
    }

    @MainActor
    func testEmptySnippetCannotBeArmed() async {
        let (queue, clipboard, _, _) = fixture()
        queue.update([Snippet(text: " \n ")])
        queue.start()
        XCTAssertEqual(queue.status, .idle)
        XCTAssertEqual(clipboard.changeCount, 0)
        XCTAssertNotNil(queue.message)
    }

    @MainActor
    func testBadgeTitleFollowsImportedCurrentSnippetThroughTransportAndCompletion() async throws {
        let (queue, clipboard, scheduler, _) = fixture()
        let markdown = """
        # Seed demo data
        ```sh
        seed-demo
        ```
        # Show results
        ```sh
        show-results
        ```
        """
        queue.update(try MarkdownImport.parse(markdown, wholeBlocks: false).map(\.snippet))
        XCTAssertEqual(BadgePresentation(queue: queue).text, "0/2")
        queue.start()
        XCTAssertEqual(BadgePresentation(queue: queue).text, "Seed demo data · 1/2")
        XCTAssertEqual(clipboard.text, "seed-demo")
        paste(queue)
        XCTAssertEqual(BadgePresentation(queue: queue).title, "Seed demo data")
        scheduler.fire()
        XCTAssertEqual(BadgePresentation(queue: queue).text, "Show results · 2/2")
        XCTAssertEqual(clipboard.text, "show-results")

        queue.pause()
        XCTAssertEqual(BadgePresentation(queue: queue).title, "Show results")
        queue.manualPrevious()
        XCTAssertEqual(BadgePresentation(queue: queue).title, "Seed demo data")
        queue.resume()
        XCTAssertEqual(BadgePresentation(queue: queue).title, "Seed demo data")
        queue.manualNext()
        queue.manualNext()
        XCTAssertEqual(queue.status, .finished)
        XCTAssertEqual(BadgePresentation(queue: queue).text, "Show results · 2/2")
        queue.start(at: queue.snippets[1].id)
        XCTAssertEqual(BadgePresentation(queue: queue).text, "Show results · 2/2")
    }

    @MainActor
    func testBadgeWithoutTitleNeverFallsBackToCommandText() async {
        let (queue, _, _, _) = fixture()
        for title in ["", " \t\r\n "] {
            queue.pause()
            queue.update([Snippet(title: title, text: "command-not-for-the-badge")])
            queue.start()
            XCTAssertEqual(BadgePresentation(queue: queue).title, "")
            XCTAssertEqual(BadgePresentation(queue: queue).text, "1/1")
        }
        queue.pause()
        queue.update([])
        XCTAssertEqual(BadgePresentation(queue: queue).text, "0/0")
    }

    @MainActor
    func testBadgeNormalizesTitleWithoutTruncatingOrChangingStoredContent() async {
        let (queue, clipboard, scheduler, url) = fixture()
        let words = Array(repeating: "Seed \u{6570}\u{636e} demo", count: 30).joined(separator: " ")
        let title = " \t\(words)\r\n  show\tresults "
        queue.update([Snippet(title: title, text: "seed-demo")])
        queue.start()
        XCTAssertEqual(BadgePresentation(queue: queue).title, "\(words) show results")
        XCTAssertEqual(queue.current?.title, title)
        let restored = DemoQueue(clipboard: FakeClipboard(), storageURL: url, schedule: scheduler.schedule)
        XCTAssertEqual(restored.snippets[0].title, title)
        XCTAssertEqual(clipboard.text, "seed-demo")
    }

    @MainActor
    func testBadgeExpansionStaysOnItsDisplayWithoutMovingItsTopEdge() {
        let workArea = NSRect(x: 1920, y: -200, width: 2560, height: 1400)
        let frame = NSRect(x: 4356, y: 1168, width: 104, height: 32)
        let expanded = FloatingBadge.resizedFrame(frame, width: 246, within: workArea)
        XCTAssertEqual(expanded.minX, 4234)
        XCTAssertEqual(expanded.maxY, frame.maxY)
        XCTAssertTrue(workArea.contains(expanded))

        let centered = NSRect(x: 2400, y: 500, width: 104, height: 32)
        let resized = FloatingBadge.resizedFrame(centered, width: 246, within: workArea)
        XCTAssertEqual(resized.origin, centered.origin)
    }

    @MainActor
    func testPreviouslySavedTitledQueueRetainsCommandText() async throws {
        let (_, clipboard, scheduler, url) = fixture()
        let oldJSON = """
        [{"id":"C6183472-34CD-478A-BBEB-C488FE257079","title":"Old title","text":"maui devflow list"}]
        """
        try Data(oldJSON.utf8).write(to: url)
        let restored = DemoQueue(clipboard: clipboard, storageURL: url, schedule: scheduler.schedule)
        XCTAssertNil(restored.storageError)
        XCTAssertEqual(restored.snippets.map(\.text), ["maui devflow list"])
        XCTAssertEqual(restored.snippets.map(\.title), ["Old title"])
    }

    @MainActor
    func testSavedQueueWithoutTitlesLoadsWithEmptyTitles() async throws {
        let (_, clipboard, scheduler, url) = fixture()
        let json = """
        [{"id":"C6183472-34CD-478A-BBEB-C488FE257079","text":"maui devflow list"}]
        """
        try Data(json.utf8).write(to: url)
        let restored = DemoQueue(clipboard: clipboard, storageURL: url, schedule: scheduler.schedule)
        XCTAssertNil(restored.storageError)
        XCTAssertEqual(restored.snippets.map(\.title), [""])
        XCTAssertEqual(restored.snippets.map(\.text), ["maui devflow list"])
    }
}
