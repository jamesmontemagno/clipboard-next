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
            let width = BadgePresentation(queue: queue).width
            let panel = NSPanel(
                contentRect: NSRect(x: 0, y: 0, width: width, height: 32),
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
            panel.setFrameAutosaveName(Self.autosaveName)
            let restored = panel.setFrameUsingName(Self.autosaveName)
            panel.setContentSize(NSSize(width: width, height: 32))
            if !restored || !Self.isOnScreen(panel.frame) {
                Self.placeAtDefault(panel)
            } else if width > 104, let screen = panel.screen {
                panel.setFrame(Self.resizedFrame(panel.frame, width: width, within: screen.visibleFrame), display: true)
            }
            self.panel = panel
            panel.contentView = DraggableHostingView(rootView: BadgeView(queue: queue, onWidthChange: { [weak self] width in
                self?.resize(width: width)
            }))
        }
        resize(width: BadgePresentation(queue: queue).width)
        panel?.orderFrontRegardless()
    }

    private func resize(width: CGFloat) {
        guard let panel, panel.frame.width != width else { return }
        var frame = panel.frame
        frame.size.width = width
        if let screen = panel.screen {
            frame = Self.resizedFrame(frame, width: width, within: screen.visibleFrame)
        }
        panel.setFrame(frame, display: true)
        panel.saveFrame(usingName: Self.autosaveName)
    }

    static func resizedFrame(_ frame: NSRect, width: CGFloat, within workArea: NSRect) -> NSRect {
        var result = frame
        result.size.width = min(width, workArea.width)
        result.origin.x = min(max(frame.minX, workArea.minX), workArea.maxX - result.width)
        result.origin.y = min(max(frame.minY, workArea.minY), workArea.maxY - result.height)
        return result
    }

    func resetPosition() {
        guard let panel else { return }
        Self.placeAtDefault(panel)
        panel.saveFrame(usingName: Self.autosaveName)
    }

    private static let autosaveName = "ClipboardNextBadge"

    private static func placeAtDefault(_ panel: NSPanel) {
        guard let screen = NSScreen.main ?? NSScreen.screens.first else { return }
        panel.setFrameOrigin(NSPoint(
            x: screen.visibleFrame.maxX - panel.frame.width - 20,
            y: screen.visibleFrame.maxY - panel.frame.height - 20))
    }

    private static func isOnScreen(_ frame: NSRect) -> Bool {
        NSScreen.screens.contains { $0.visibleFrame.intersects(frame) }
    }
}

struct BadgePresentation {
    let title: String
    let position: String
    var width: CGFloat { title.isEmpty ? 104 : 246 }
    var text: String { title.isEmpty ? position : "\(title) · \(position)" }

    @MainActor
    init(queue: DemoQueue) {
        title = (queue.current?.title ?? "").split(whereSeparator: \.isWhitespace).joined(separator: " ")
        position = queue.index.map { "\($0 + 1)/\(queue.snippets.count)" } ?? "0/\(queue.snippets.count)"
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
    let onWidthChange: (CGFloat) -> Void
    @State private var flash = false

    var body: some View {
        let presentation = BadgePresentation(queue: queue)
        HStack(spacing: 7) {
            Image(systemName: flash ? "checkmark.circle.fill" : "doc.on.clipboard")
                .font(.caption)
                .foregroundStyle(flash ? Color.green : Color.accentColor)
            if !presentation.title.isEmpty {
                Text(presentation.title)
                    .font(.system(.caption, weight: .semibold))
                    .lineLimit(1)
                    .truncationMode(.tail)
                    .frame(maxWidth: .infinity, alignment: .leading)
                Text("·")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Text(presentation.position)
                .font(.system(.caption, design: .rounded, weight: .semibold))
                .monospacedDigit()
                .fixedSize()
            Circle()
                .fill(queue.status == .running ? Color.green : Color.secondary)
                .frame(width: 5, height: 5)
        }
        .padding(.horizontal, 10)
        .frame(width: presentation.width, height: 32)
        .background(.regularMaterial, in: Capsule())
        .overlay {
            Capsule()
                .strokeBorder(flash ? Color.green : Color.primary.opacity(0.12), lineWidth: flash ? 2 : 1)
        }
        .contentShape(Capsule())
        .help("\(presentation.text). \(flash ? "Paste detected" : queue.status.rawValue). Drag anywhere to move; hide using Show Floating Badge.")
        .onChange(of: presentation.width) { _, width in onWidthChange(width) }
        .task(id: queue.detectedPastes) {
            guard let detected = queue.lastDetectedPaste, Date().timeIntervalSince(detected) < 0.9 else { return }
            flash = true
            do { try await Task.sleep(for: .milliseconds(900)) }
            catch { return }
            flash = false
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(presentation.title.isEmpty ? "" : "\(presentation.title), ")\(queue.progress), \(flash ? "Paste detected" : queue.status.rawValue)")
    }
}
