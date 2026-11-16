import AppKit
import XCTest
@testable import ClipboardNext

final class ThemeSettingsTests: XCTestCase {
    @MainActor
    func testThemesPersistAndApplyImmediatelyIncludingReturningToSystem() async {
        let suite = "ClipboardNextThemeTests-\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suite)!
        defer { defaults.removePersistentDomain(forName: suite) }
        var applied: [NSAppearance.Name?] = []
        let settings = ThemeSettings(defaults: defaults) { applied.append($0?.name) }
        XCTAssertEqual(settings.theme, .system)
        XCTAssertEqual(applied.count, 1)
        XCTAssertNil(applied.last!)

        for theme in [AppTheme.light, .dark, .system] {
            settings.theme = theme
            XCTAssertEqual(defaults.string(forKey: "appTheme"), theme.rawValue)
            var restoredAppearance: NSAppearance.Name?
            let restored = ThemeSettings(defaults: defaults) { restoredAppearance = $0?.name }
            XCTAssertEqual(restored.theme, theme)
            XCTAssertEqual(restoredAppearance, theme.appearance?.name)
        }

        XCTAssertEqual(applied.count, 4)
        XCTAssertEqual(applied[1], .aqua)
        XCTAssertEqual(applied[2], .darkAqua)
        XCTAssertNil(applied[3])
        settings.theme = .system
        XCTAssertEqual(applied.count, 4)
    }

    @MainActor
    func testUnknownSavedThemeFollowsSystemWithoutChangingOtherPreferences() async {
        let suite = "ClipboardNextThemeTests-\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suite)!
        defer { defaults.removePersistentDomain(forName: suite) }
        defaults.set("future-theme", forKey: "appTheme")
        defaults.set(true, forKey: "showFloatingBadge")
        var appliedAppearance: NSAppearance? = NSAppearance(named: .darkAqua)
        let settings = ThemeSettings(defaults: defaults) { appliedAppearance = $0 }

        XCTAssertEqual(settings.theme, .system)
        XCTAssertNil(appliedAppearance)
        settings.theme = .light
        XCTAssertTrue(defaults.bool(forKey: "showFloatingBadge"))
    }
}
