import AppKit
import Observation

enum AppTheme: String, CaseIterable, Identifiable {
    case system
    case light
    case dark

    var id: String { rawValue }

    var label: String {
        switch self {
        case .system: "System"
        case .light: "Light"
        case .dark: "Dark"
        }
    }

    var appearance: NSAppearance? {
        switch self {
        case .system: nil
        case .light: NSAppearance(named: .aqua)
        case .dark: NSAppearance(named: .darkAqua)
        }
    }
}

@MainActor
@Observable
final class ThemeSettings {
    var theme: AppTheme {
        didSet {
            guard theme != oldValue else { return }
            defaults.set(theme.rawValue, forKey: "appTheme")
            applyAppearance(theme.appearance)
        }
    }

    @ObservationIgnored private let defaults: UserDefaults
    @ObservationIgnored private let applyAppearance: (NSAppearance?) -> Void

    init(
        defaults: UserDefaults = .standard,
        applyAppearance: @escaping (NSAppearance?) -> Void = { NSApplication.shared.appearance = $0 }
    ) {
        self.defaults = defaults
        self.applyAppearance = applyAppearance
        theme = AppTheme(rawValue: defaults.string(forKey: "appTheme") ?? "") ?? .system
        applyAppearance(theme.appearance)
    }
}
