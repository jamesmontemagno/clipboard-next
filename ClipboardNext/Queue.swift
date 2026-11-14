import Foundation
import Observation

struct Snippet: Identifiable, Codable, Equatable {
    var id = UUID()
    var title = ""
    var text: String

    init(id: UUID = UUID(), title: String = "", text: String) {
        self.id = id
        self.title = title
        self.text = text
    }

    // Queues saved before titles existed have no "title" key.
    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(UUID.self, forKey: .id)
        title = try container.decodeIfPresent(String.self, forKey: .title) ?? ""
        text = try container.decode(String.self, forKey: .text)
    }
}

enum QueueStatus: String {
    case idle = "Ready"
    case running = "Running"
    case paused = "Paused"
    case finished = "Finished"
}

@MainActor
protocol ClipboardAccess: AnyObject {
    var changeCount: Int { get }
    func write(_ text: String) throws
}

enum ClipboardError: LocalizedError {
    case writeFailed
    var errorDescription: String? { "Could not write to the clipboard. The queue has been paused." }
}

@MainActor
@Observable
final class DemoQueue {
    private(set) var snippets: [Snippet] = []
    private(set) var index: Int?
    private(set) var status: QueueStatus = .idle
    var message: String?
    var storageError: String?
    private(set) var ownsClipboard = false
    private(set) var detectedPastes = 0
    private(set) var lastDetectedPaste: Date?
    /// Wait after Command-V is released before the next snippet replaces the clipboard.
    private(set) var advanceDelayMilliseconds: Int

    static let delayRange = 100...3000
    static let defaultDelay = 350
    private static let delayKey = "advanceDelayMilliseconds"

    @ObservationIgnored private let clipboard: ClipboardAccess
    @ObservationIgnored private let storageURL: URL
    @ObservationIgnored private var ownedChangeCount: Int?
    @ObservationIgnored private var pending: (() -> Void)?
    @ObservationIgnored private let schedule: @MainActor (Int, @escaping @MainActor () -> Void) -> (() -> Void)
    @ObservationIgnored private let defaults: UserDefaults
    @ObservationIgnored private var generation = 0
    @ObservationIgnored private var pasteKeyDown = false

    var current: Snippet? {
        guard let index, snippets.indices.contains(index) else { return nil }
        return snippets[index]
    }

    var next: Snippet? {
        guard let index, snippets.indices.contains(index + 1) else { return nil }
        return snippets[index + 1]
    }

    var progress: String {
        guard let index else { return "\(snippets.count) snippets" }
        return "\(index + 1) of \(snippets.count)"
    }

    init(
        clipboard: ClipboardAccess, storageURL: URL, defaults: UserDefaults = .standard,
        schedule: @escaping @MainActor (Int, @escaping @MainActor () -> Void) -> (() -> Void) = DemoQueue.schedulePaste
    ) {
        self.clipboard = clipboard
        self.storageURL = storageURL
        self.defaults = defaults
        self.schedule = schedule
        let saved = defaults.object(forKey: Self.delayKey) as? Int
        advanceDelayMilliseconds = Self.clampDelay(saved ?? Self.defaultDelay)
        do {
            if FileManager.default.fileExists(atPath: storageURL.path) {
                snippets = try JSONDecoder().decode([Snippet].self, from: Data(contentsOf: storageURL))
            }
        } catch {
            storageError = "Could not load your saved queue: \(error.localizedDescription). The saved file has not been replaced."
        }
    }

    func update(_ items: [Snippet]) {
        guard status != .running else {
            message = "Pause before editing the queue."
            return
        }
        let currentID = current?.id
        cancelPending()
        snippets = items
        index = currentID.flatMap { id in items.firstIndex { $0.id == id } }
        status = .idle
        ownsClipboard = false
        save()
    }

    func save() {
        // A failed load must not silently overwrite the original file.
        guard storageError == nil else { return }
        do {
            try FileManager.default.createDirectory(
                at: storageURL.deletingLastPathComponent(), withIntermediateDirectories: true)
            try JSONEncoder().encode(snippets).write(to: storageURL, options: .atomic)
        } catch {
            storageError = "Could not save your queue: \(error.localizedDescription)"
        }
    }

    func retrySave() {
        storageError = nil
        save()
    }

    func start(at selectedID: UUID? = nil) {
        cancelPending()
        guard !snippets.isEmpty else {
            message = "Add or import a snippet first."
            return
        }
        guard snippets.allSatisfy({ !$0.text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }) else {
            message = "Remove or fill in empty snippets before starting."
            return
        }
        index = selectedID.flatMap { id in snippets.firstIndex { $0.id == id } } ?? 0
        message = nil
        status = .running
        copyCurrent()
    }

    func pause(_ reason: String? = nil) {
        cancelPending()
        if status == .running { status = .paused }
        if let reason { message = reason }
    }

    func reset() {
        cancelPending()
        index = nil
        status = .idle
        ownsClipboard = false
        ownedChangeCount = nil
        message = nil
    }

    func resume() {
        guard current != nil, status == .paused else {
            start()
            return
        }
        cancelPending()
        message = nil
        status = .running
        copyCurrent()
    }

    func manualNext() {
        guard current != nil else {
            start()
            return
        }
        cancelPending()
        advance()
    }

    var canGoBack: Bool { (index ?? 0) > 0 }

    func manualPrevious() {
        guard let index, index > 0 else { return }
        cancelPending()
        self.index = index - 1
        // Stepping back from the end reopens the queue so the next paste advances again.
        if status == .finished {
            status = .running
            message = nil
        }
        copyCurrent()
    }

    func checkClipboard() {
        guard let ownedChangeCount, clipboard.changeCount != ownedChangeCount else { return }
        ownsClipboard = false
        if status == .running {
            pause("Another app changed the clipboard. Resume to copy the current snippet again.")
        }
    }

    func keyDown(command: Bool, isRepeat: Bool, isOwnApp: Bool) {
        guard status == .running, command, !isRepeat, !isOwnApp, !pasteKeyDown, pending == nil else { return }
        checkClipboard()
        guard status == .running else { return }
        pasteKeyDown = true
    }

    func keyUp() {
        guard pasteKeyDown else { return }
        pasteKeyDown = false
        let token = generation
        pending = schedule(advanceDelayMilliseconds) { [weak self] in
            self?.completePaste(generation: token)
        }
    }

    func setAdvanceDelay(milliseconds: Int) {
        advanceDelayMilliseconds = Self.clampDelay(milliseconds)
        defaults.set(advanceDelayMilliseconds, forKey: Self.delayKey)
    }

    private static func clampDelay(_ value: Int) -> Int {
        min(max(value, delayRange.lowerBound), delayRange.upperBound)
    }

    private static func schedulePaste(
        _ milliseconds: Int, _ action: @escaping @MainActor () -> Void
    ) -> (() -> Void) {
        let task = Task {
            do { try await Task.sleep(for: .milliseconds(milliseconds)) }
            catch { return }
            action()
        }
        return { task.cancel() }
    }

    private func completePaste(generation token: Int) {
        guard token == generation, status == .running else { return }
        pending = nil
        checkClipboard()
        guard status == .running else { return }
        detectedPastes += 1
        lastDetectedPaste = Date()
        advance()
    }

    private func advance() {
        guard let index, snippets.indices.contains(index + 1) else {
            status = .finished
            message = "All snippets completed. The final snippet is still on the clipboard."
            return
        }
        self.index = index + 1
        copyCurrent()
    }

    private func copyCurrent() {
        guard let current else { return }
        do {
            try clipboard.write(current.text)
            ownedChangeCount = clipboard.changeCount
            ownsClipboard = true
        } catch {
            ownsClipboard = false
            pause(error.localizedDescription)
        }
    }

    private func cancelPending() {
        generation += 1
        pending?()
        pending = nil
        pasteKeyDown = false
    }
}
