import AppKit
import SwiftUI
import Observation

@MainActor
@Observable
final class FloatingBadge {
    var isVisible: Bool {
        didSet {
            UserDefaults.standard.set(isVisible, forKey: "showFloatingBadge")
            if isVisible { show() } else { panel?.orderOut(nil) }
        }
    }
    @ObservationIgnored private var panel: NSPanel?
    @ObservationIgnored private let queue: DemoQueue

    init(queue: DemoQueue) {
        self.queue = queue
        isVisible = UserDefaults.standard.bool(forKey: "showFloatingBadge")
        if isVisible { show() }
    }

    private func show() {
        if panel == nil {
            let panel = NSPanel(
                contentRect: NSRect(x: 0, y: 0, width: 104, height: 32),
                styleMask: [.borderless, .nonactivatingPanel],
                backing: .buffered, defer: false)
            panel.level = .floating
            panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
            panel.isMovableByWindowBackground = true
            panel.isFloatingPanel = true
            panel.hidesOnDeactivate = false
            panel.isOpaque = false
            panel.backgroundColor = .clear
            panel.hasShadow = true
            panel.contentView = DraggableHostingView(rootView: BadgeView(queue: queue))
            panel.setFrameAutosaveName(Self.autosaveName)
            let restored = panel.setFrameUsingName(Self.autosaveName)
            panel.setContentSize(NSSize(width: 104, height: 32))
            if !restored || !Self.isOnScreen(panel.frame) {
                Self.placeAtDefault(panel)
            }
            self.panel = panel
        }
        panel?.orderFrontRegardless()
    }

    func resetPosition() {
        guard let panel else { return }
        Self.placeAtDefault(panel)
        panel.saveFrame(usingName: Self.autosaveName)
    }

    private static let autosaveName = "ClipboardNextBadge"

    private static func placeAtDefault(_ panel: NSPanel) {
        guard let screen = NSScreen.main ?? NSScreen.screens.first else { return }
        panel.setFrameOrigin(NSPoint(x: screen.visibleFrame.maxX - 124, y: screen.visibleFrame.maxY - 52))
    }

    private static func isOnScreen(_ frame: NSRect) -> Bool {
        NSScreen.screens.contains { $0.visibleFrame.intersects(frame) }
    }
}

/// SwiftUI hosting views swallow mouse events, so `isMovableByWindowBackground` alone doesn't let the
/// borderless badge be dragged. Start the window drag explicitly instead.
private final class DraggableHostingView<Content: View>: NSHostingView<Content> {
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
    override var mouseDownCanMoveWindow: Bool { true }

    override func mouseDown(with event: NSEvent) {
        window?.performDrag(with: event)
    }
}

private struct BadgeView: View {
    let queue: DemoQueue
    @State private var flash = false

    var body: some View {
        HStack(spacing: 7) {
            Image(systemName: flash ? "checkmark.circle.fill" : "doc.on.clipboard")
                .font(.caption)
                .foregroundStyle(flash ? Color.green : Color.accentColor)
            Text(queue.index.map { "\($0 + 1)/\(queue.snippets.count)" } ?? "0/\(queue.snippets.count)")
                .font(.system(.caption, design: .rounded, weight: .semibold))
                .monospacedDigit()
            Circle()
                .fill(queue.status == .running ? Color.green : Color.secondary)
                .frame(width: 5, height: 5)
        }
        .padding(.horizontal, 10)
        .frame(width: 104, height: 32)
        .background(.regularMaterial, in: Capsule())
        .overlay {
            Capsule()
                .strokeBorder(flash ? Color.green : Color.primary.opacity(0.12), lineWidth: flash ? 2 : 1)
        }
        .contentShape(Capsule())
        .help("\(flash ? "Paste detected" : queue.status.rawValue). Drag anywhere to move; hide using Show Floating Badge.")
        .task(id: queue.detectedPastes) {
            guard let detected = queue.lastDetectedPaste, Date().timeIntervalSince(detected) < 0.9 else { return }
            flash = true
            do { try await Task.sleep(for: .milliseconds(900)) }
            catch { return }
            flash = false
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(queue.progress), \(flash ? "Paste detected" : queue.status.rawValue)")
    }
}
