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
@Observable
final class AppModel {
    let queue: DemoQueue
    let monitor: ClipboardMonitor
    let badge: FloatingBadge
    var showingSettings = false

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
    var expanded = false

    var body: some View {
        HStack {
            Button("Back", systemImage: "backward.end.fill") { queue.manualPrevious() }
                .frame(maxWidth: expanded ? .infinity : nil)
                .disabled(!queue.canGoBack)
                .help("Put the previous snippet back on the clipboard")
            if queue.status == .running {
                Button("Pause", systemImage: "pause.fill") { queue.pause() }
                    .frame(maxWidth: expanded ? .infinity : nil)
                    .buttonStyle(.borderedProminent)
            } else if queue.status == .paused {
                Button("Resume", systemImage: "play.fill") { queue.resume() }
                    .frame(maxWidth: expanded ? .infinity : nil)
                    .buttonStyle(.borderedProminent)
            } else {
                Button("Start", systemImage: "play.fill") { queue.start(at: selectedID) }
                    .frame(maxWidth: expanded ? .infinity : nil)
                    .buttonStyle(.borderedProminent)
            }
            Button("Next", systemImage: "forward.end.fill") { queue.manualNext() }
                .frame(maxWidth: expanded ? .infinity : nil)
                .disabled(queue.snippets.isEmpty || queue.status == .finished)
        }
        .buttonStyle(.bordered)
    }
}

struct MenuView: View {
    let model: AppModel
    @Environment(\.openWindow) private var openWindow
    @Environment(\.dismiss) private var dismiss
    @State private var showingSettings = false
    @AppStorage("hasSeenPasteDetectionWelcome") private var hasSeenWelcome = false

    var body: some View {
        Group {
            if showingSettings {
                SettingsView(model: model, inMenu: true) { showingSettings = false }
            } else {
                mainPanel
            }
        }
        .onDisappear { showingSettings = false }
    }

    private var mainPanel: some View {
        @Bindable var badge = model.badge
        return VStack(alignment: .leading, spacing: 16) {
            HStack(spacing: 10) {
                Image(systemName: "clipboard")
                    .font(.title2).foregroundStyle(.tint)
                    .frame(width: 38, height: 38)
                    .background(.tint.opacity(0.1), in: RoundedRectangle(cornerRadius: 10))
                VStack(alignment: .leading, spacing: 3) {
                    Text("Clipboard Next").font(.headline)
                    Text(model.queue.progress).font(.caption).foregroundStyle(.secondary)
                }
                Spacer()
                Button {
                    model.queue.reset()
                } label: {
                    Image(systemName: "arrow.counterclockwise")
                }
                .buttonStyle(.borderless)
                .disabled(model.queue.index == nil && model.queue.status == .idle)
                .help("Reset to the beginning without changing snippets or the clipboard")
                .accessibilityLabel("Reset Queue")
            }
            if !model.monitor.monitoring && !hasSeenWelcome {
                welcomeCard
            }
            HStack {
                Label(model.queue.status.rawValue,
                      systemImage: model.queue.status == .running ? "play.circle.fill" : "circle")
                    .font(.caption.weight(.medium))
                    .foregroundStyle(model.queue.status == .running ? Color.green : Color.secondary)
                Spacer()
                Text(BadgePresentation(queue: model.queue).position)
                    .font(.caption.monospacedDigit()).foregroundStyle(.secondary)
            }
            ProgressView(value: Double(model.queue.index.map { $0 + 1 } ?? 0),
                         total: Double(max(model.queue.snippets.count, 1)))
                .accessibilityLabel("Queue progress")
            preview("Current", snippet: model.queue.current)
            preview("Up next", snippet: model.queue.next)
            QueueControls(queue: model.queue, expanded: true)
            if let message = model.queue.message {
                Text(message).font(.caption).foregroundStyle(.secondary)
            }
            if model.queue.storageError != nil {
                Label("Queue could not be saved or loaded. Open the editor.", systemImage: "exclamationmark.triangle")
                    .font(.caption).foregroundStyle(.orange)
            }
            if !model.monitor.monitoring && hasSeenWelcome {
                Label("Manual mode · keyboard monitoring is off", systemImage: "hand.raised")
                    .font(.caption).foregroundStyle(.orange)
                Button("Enable paste detection in Settings…") { showingSettings = true }
                    .buttonStyle(.link)
                    .font(.caption)
            }
            Divider()
            HStack(spacing: 12) {
                Button { showingSettings = true } label: {
                    Image(systemName: "gearshape").font(.title3)
                }
                .help("Settings")
                .accessibilityLabel("Settings")
                Button { openEditor() } label: {
                    Label("Open Editor", systemImage: "pencil")
                }
                .foregroundStyle(.primary)
                Spacer()
                Button { badge.isVisible.toggle() } label: {
                    Label("Badge", systemImage: badge.isVisible ? "rectangle.on.rectangle.fill" : "rectangle.on.rectangle")
                        .foregroundStyle(.white)
                }
                .buttonStyle(.borderedProminent)
                .tint(badge.isVisible ? Color.accentColor : Color.gray)
                .help(badge.isVisible ? "Hide the floating badge" : "Show the floating badge")
                .accessibilityValue(badge.isVisible ? "On" : "Off")
                Button {
                    model.monitor.stop()
                    NSApp.terminate(nil)
                } label: {
                    Image(systemName: "power").font(.title3)
                }
                .help("Quit Clipboard Next")
                .accessibilityLabel("Quit Clipboard Next")
            }
            .buttonStyle(.borderless)
            .foregroundStyle(.secondary)
        }
        .padding(20)
        .frame(width: 380)
    }

    private var welcomeCard: some View {
        VStack(alignment: .leading, spacing: 8) {
            Label("Advance automatically when you paste", systemImage: "keyboard")
                .font(.subheadline.weight(.semibold))
            Text("Clipboard Next can move to the next snippet each time you press Command-V. macOS will ask you to allow Input Monitoring. Only the V key is checked, and nothing leaves your Mac.")
                .font(.caption).foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)
            HStack {
                Button("Enable Paste Detection") {
                    hasSeenWelcome = true
                    model.monitor.requestPermission()
                }
                .buttonStyle(.borderedProminent)
                Button("Not Now") { hasSeenWelcome = true }
                    .buttonStyle(.bordered)
            }
            .controlSize(.small)
        }
        .padding(12)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(.tint.opacity(0.08), in: RoundedRectangle(cornerRadius: 10))
    }

    private func openEditor(showSettings: Bool = false) {
        dismiss()
        model.showingSettings = showSettings
        openWindow(id: "editor")
        NSApp.activate(ignoringOtherApps: true)
    }

    private func preview(_ label: String, snippet: Snippet?) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(label.uppercased()).font(.caption2.weight(.semibold)).foregroundStyle(.secondary)
            if let snippet, !snippet.title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                Text(snippet.title).font(.subheadline.weight(.semibold)).lineLimit(1)
            }
            Text(snippet?.text ?? (label == "Current" ? "Press Start to begin your queue." : "No next snippet."))
                .font(.system(.caption, design: .monospaced))
                .lineLimit(3)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(12)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(.quaternary.opacity(0.5), in: RoundedRectangle(cornerRadius: 12))
    }
}
