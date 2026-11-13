import AppKit
import SwiftUI

@main
struct ClipboardNextApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var delegate
    @State private var model = AppModel()

    var body: some Scene {
        MenuBarExtra {
            MenuView(model: model)
        } label: {
            MenuBarIndicator(queue: model.queue)
        }
        .menuBarExtraStyle(.window)

        Window("Clipboard Next", id: "editor") {
            EditorView(model: model)
                .frame(minWidth: 820, minHeight: 540)
        }
        .defaultSize(width: 960, height: 650)
    }
}

private struct MenuBarIndicator: View {
    let queue: DemoQueue
    @State private var flash = false

    var body: some View {
        Label(
            queue.status == .running ? queue.progress : "Clipboard Next",
            systemImage: flash ? "checkmark.circle.fill" : queue.status == .running ? "clipboard.fill" : "clipboard")
        .task(id: queue.detectedPastes) {
            guard let detected = queue.lastDetectedPaste, Date().timeIntervalSince(detected) < 0.9 else { return }
            flash = true
            do { try await Task.sleep(for: .milliseconds(900)) }
            catch { return }
            flash = false
        }
    }
}

@MainActor
final class AppModel {
    let queue: DemoQueue
    let monitor: ClipboardMonitor
    let badge: FloatingBadge

    init() {
        let url = URL.applicationSupportDirectory
            .appendingPathComponent("ClipboardNext", isDirectory: true)
            .appendingPathComponent("queue.json")
        let queue = DemoQueue(clipboard: SystemClipboard(), storageURL: url)
        self.queue = queue
        monitor = ClipboardMonitor(queue: queue)
        badge = FloatingBadge(queue: queue)
        if ProcessInfo.processInfo.environment["XCTestConfigurationFilePath"] == nil {
            monitor.start()
        }
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { false }
}

struct QueueControls: View {
    let queue: DemoQueue
    var selectedID: UUID?

    var body: some View {
        HStack {
            Button("Back", systemImage: "backward.end.fill") { queue.manualPrevious() }
                .disabled(!queue.canGoBack)
                .help("Put the previous snippet back on the clipboard")
            if queue.status == .running {
                Button("Pause", systemImage: "pause.fill") { queue.pause() }
            } else if queue.status == .paused {
                Button("Resume", systemImage: "play.fill") { queue.resume() }
            } else {
                Button("Start", systemImage: "play.fill") { queue.start(at: selectedID) }
            }
            Button("Next", systemImage: "forward.end.fill") { queue.manualNext() }
                .disabled(queue.snippets.isEmpty || queue.status == .finished)
        }
        .buttonStyle(.bordered)
    }
}

struct MenuView: View {
    let model: AppModel
    @Environment(\.openWindow) private var openWindow
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        @Bindable var badge = model.badge
        VStack(alignment: .leading, spacing: 14) {
            HStack {
                Label("Clipboard Next", systemImage: "clipboard")
                    .font(.headline)
                Spacer()
                Text(model.queue.status.rawValue).foregroundStyle(.secondary)
            }
            Text(model.queue.progress).font(.system(.title2, design: .rounded).bold())
            preview("Current", snippet: model.queue.current)
            preview("Up next", snippet: model.queue.next)
            QueueControls(queue: model.queue)
            if let message = model.queue.message {
                Text(message).font(.caption).foregroundStyle(.secondary)
            }
            if model.queue.storageError != nil {
                Label("Queue could not be saved or loaded. Open the editor.", systemImage: "exclamationmark.triangle")
                    .font(.caption).foregroundStyle(.orange)
            }
            if !model.monitor.monitoring {
                Label("Manual mode · keyboard monitoring is off", systemImage: "hand.raised")
                    .font(.caption).foregroundStyle(.orange)
                Button("Enable Paste Detection") { model.monitor.requestPermission() }
                Text(model.monitor.error ?? "Open the editor for permission setup.")
                    .font(.caption).foregroundStyle(.secondary)
                    .lineLimit(5)
            }
            Divider()
            Toggle("Show Floating Badge", isOn: $badge.isVisible)
            if badge.isVisible {
                Button("Reset Badge Position") { badge.resetPosition() }
            }
            Button("Open Queue Editor…") {
                dismiss()
                openWindow(id: "editor")
                NSApp.activate(ignoringOtherApps: true)
            }
            Button("Quit Clipboard Next") {
                model.monitor.stop()
                NSApp.terminate(nil)
            }
        }
        .padding(18)
        .frame(width: 340)
    }

    private func preview(_ label: String, snippet: Snippet?) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(label.uppercased()).font(.caption2.weight(.semibold)).foregroundStyle(.secondary)
            Text(snippet?.text ?? "—")
                .font(.system(.caption, design: .monospaced))
                .lineLimit(3)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
    }
}
