import AppKit
import Observation

@MainActor
final class SystemClipboard: ClipboardAccess {
    var changeCount: Int { NSPasteboard.general.changeCount }

    func write(_ text: String) throws {
        NSPasteboard.general.clearContents()
        guard NSPasteboard.general.setString(text, forType: .string) else {
            throw ClipboardError.writeFailed
        }
    }
}

@MainActor
@Observable
final class ClipboardMonitor {
    private(set) var permissionGranted = false
    private(set) var monitoring = false
    private(set) var error: String?
    @ObservationIgnored private var tap: CFMachPort?
    @ObservationIgnored private var source: CFRunLoopSource?
    @ObservationIgnored private var timer: Timer?
    @ObservationIgnored private let queue: DemoQueue
    @ObservationIgnored private let requestAccess: @MainActor () -> Bool
    @ObservationIgnored private let settingsOpener: @MainActor () -> Bool

    init(
        queue: DemoQueue,
        requestAccess: @escaping @MainActor () -> Bool = { CGRequestListenEventAccess() },
        settingsOpener: @escaping @MainActor () -> Bool = ClipboardMonitor.openPrivacySettings
    ) {
        self.queue = queue
        self.requestAccess = requestAccess
        self.settingsOpener = settingsOpener
    }

    func start() {
        permissionGranted = CGPreflightListenEventAccess()
        if permissionGranted { installTap() }
        else { error = Self.permissionHelp }
        timer = Timer.scheduledTimer(withTimeInterval: 0.2, repeats: true) { [weak self] _ in
            Task { @MainActor in
                guard let self else { return }
                self.queue.checkClipboard()
                let granted = CGPreflightListenEventAccess()
                if granted != self.permissionGranted {
                    self.permissionGranted = granted
                    if granted {
                        self.installTap()
                    } else {
                        self.removeTap()
                        self.error = Self.permissionHelp
                        self.queue.pause("Input Monitoring permission was removed. Manual Next is still available.")
                    }
                }
            }
        }
    }

    func requestPermission() {
        defer { openSettings() }
        if requestAccess() {
            permissionGranted = true
            installTap()
        } else {
            error = Self.permissionHelp
        }
    }

    func openSettings() {
        guard settingsOpener() else {
            error = "Open System Settings → Privacy & Security → Input Monitoring."
            return
        }
    }

    private static func openPrivacySettings() -> Bool {
        guard let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_ListenEvent") else {
            return false
        }
        return NSWorkspace.shared.open(url)
    }

    func stop() {
        timer?.invalidate()
        timer = nil
        queue.pause()
        removeTap()
    }

    private func installTap() {
        guard tap == nil else { return }
        let mask = (CGEventMask(1) << CGEventType.keyDown.rawValue)
            | (CGEventMask(1) << CGEventType.keyUp.rawValue)
        let callback: CGEventTapCallBack = { _, type, event, context in
            guard let context else { return Unmanaged.passUnretained(event) }
            // The source is installed only on the main run loop.
            MainActor.assumeIsolated {
                let monitor = Unmanaged<ClipboardMonitor>.fromOpaque(context).takeUnretainedValue()
                monitor.receive(type: type, event: event)
            }
            return Unmanaged.passUnretained(event)
        }
        guard let tap = CGEvent.tapCreate(
            tap: .cgSessionEventTap, place: .tailAppendEventTap, options: .listenOnly,
            eventsOfInterest: mask, callback: callback,
            userInfo: Unmanaged.passUnretained(self).toOpaque()) else {
            error = "Keyboard monitoring could not start. Check Input Monitoring permission, then retry."
            queue.pause(error)
            return
        }
        guard let source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0) else {
            CFMachPortInvalidate(tap)
            error = "Could not create the keyboard monitor run-loop source."
            queue.pause(error)
            return
        }
        self.tap = tap
        self.source = source
        CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
        CGEvent.tapEnable(tap: tap, enable: true)
        monitoring = true
        error = nil
    }

    private func receive(type: CGEventType, event: CGEvent) {
        if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
            error = "macOS disabled keyboard monitoring. The queue is paused; retry monitoring to continue automatically."
            queue.pause(error)
            removeTap()
            return
        }
        // Command-V uses the physical V key (ANSI key code 9).
        guard event.getIntegerValueField(.keyboardEventKeycode) == 9 else { return }
        if type == .keyDown {
            queue.keyDown(
                command: event.flags.contains(.maskCommand)
                    && !event.flags.contains(.maskControl) && !event.flags.contains(.maskAlternate)
                    && !event.flags.contains(.maskShift),
                isRepeat: event.getIntegerValueField(.keyboardEventAutorepeat) != 0,
                isOwnApp: NSWorkspace.shared.frontmostApplication?.processIdentifier == ProcessInfo.processInfo.processIdentifier)
        } else if type == .keyUp {
            queue.keyUp()
        }
    }

    private func removeTap() {
        if let source { CFRunLoopRemoveSource(CFRunLoopGetMain(), source, .commonModes) }
        if let tap { CFMachPortInvalidate(tap) }
        source = nil
        tap = nil
        monitoring = false
    }

    private static let permissionHelp = "Input Monitoring is not granted to this build. Enable this copy in System Settings → Privacy & Security → Input Monitoring, then quit and reopen it. If it is already listed after rebuilding, remove the stale entry and add this copy again using Reveal This App. Debug and Release need separate approvals. Manual Next still works."
}
