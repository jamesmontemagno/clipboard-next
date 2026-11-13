import XCTest
@testable import ClipboardNext

final class ClipboardMonitorTests: XCTestCase {
    @MainActor
    func testEveryEnableAttemptOpensSettingsEvenAfterDeniedPermission() async {
        let queue = DemoQueue(
            clipboard: FakeClipboard(),
            storageURL: FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString))
        var permissionRequests = 0
        var settingsOpens = 0
        let monitor = ClipboardMonitor(
            queue: queue,
            requestAccess: { permissionRequests += 1; return false },
            settingsOpener: { settingsOpens += 1; return true })
        monitor.requestPermission()
        monitor.requestPermission()
        XCTAssertEqual(permissionRequests, 2)
        XCTAssertEqual(settingsOpens, 2)
        XCTAssertFalse(monitor.monitoring)
        XCTAssertNotNil(monitor.error)
    }

    @MainActor
    func testFailedSettingsOpenShowsManualInstructions() async {
        let queue = DemoQueue(
            clipboard: FakeClipboard(),
            storageURL: FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString))
        let monitor = ClipboardMonitor(queue: queue, requestAccess: { false }, settingsOpener: { false })
        monitor.requestPermission()
        XCTAssertTrue(monitor.error?.contains("System Settings") == true)
        XCTAssertFalse(monitor.monitoring)
    }
}
