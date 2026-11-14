import AppKit
import ServiceManagement
import SwiftUI

struct SettingsButton: View {
    let model: AppModel

    var body: some View {
        @Bindable var model = model
        Button("Settings", systemImage: "gearshape") { model.showingSettings = true }
            .help("Open settings")
            .popover(isPresented: $model.showingSettings, arrowEdge: .bottom) {
                SettingsView(model: model) { model.showingSettings = false }
            }
    }
}

struct SettingsView: View {
    let model: AppModel
    var inMenu = false
    let onClose: () -> Void
    @Environment(\.scenePhase) private var scenePhase
    @State private var loginStatus = SMAppService.mainApp.status
    @State private var loginError: String?

    var body: some View {
        @Bindable var badge = model.badge
        VStack(spacing: 0) {
            HStack {
                if inMenu {
                    Button(action: onClose) {
                        Image(systemName: "chevron.left").font(.headline)
                    }
                    .buttonStyle(.borderless)
                    .help("Back")
                    .accessibilityLabel("Back")
                }
                Label("Settings", systemImage: "gearshape").font(.headline)
                Spacer()
                if !inMenu {
                    Button("Done", action: onClose)
                }
            }
            .padding(20)
            Divider()
            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    section("Floating Badge", icon: "rectangle.on.rectangle") {
                        Toggle("Show floating badge", isOn: $badge.isVisible)
                            .toggleStyle(.switch)
                        Text("Keep the current title and progress visible without taking keyboard focus.")
                            .font(.caption).foregroundStyle(.secondary)
                        Button("Reset Badge Position") { badge.resetPosition() }
                            .disabled(!badge.isVisible)
                    }
                    Divider()
                    section("Paste Detection", icon: "keyboard") {
                        Label(model.monitor.monitoring ? "Paste detection on" : "Manual mode",
                              systemImage: model.monitor.monitoring ? "checkmark.circle.fill" : "hand.raised")
                            .foregroundStyle(model.monitor.monitoring ? Color.green : Color.secondary)
                        Text("Advance after Command-V. Input Monitoring approval is required; manual Next always works.")
                            .font(.caption).foregroundStyle(.secondary)
                        HStack {
                            Button(model.monitor.monitoring ? "Privacy Settings" : "Enable / Retry") {
                                if model.monitor.monitoring { model.monitor.openSettings() }
                                else { model.monitor.requestPermission() }
                            }
                            Button("Reveal This App") {
                                NSWorkspace.shared.activateFileViewerSelecting([Bundle.main.bundleURL])
                            }
                        }
                        Text("Running: \(Bundle.main.object(forInfoDictionaryKey: "CFBundleDisplayName") as? String ?? "Clipboard Next")")
                            .font(.caption).foregroundStyle(.secondary)
                        if let error = model.monitor.error {
                            Text(error).font(.caption).foregroundStyle(.orange).textSelection(.enabled)
                        }
                        Text("Next after").font(.subheadline)
                        HStack {
                            Slider(
                                value: Binding(
                                    get: { Double(model.queue.advanceDelayMilliseconds) },
                                    set: { model.queue.setAdvanceDelay(milliseconds: Int($0)) }),
                                in: Double(DemoQueue.delayRange.lowerBound)...Double(DemoQueue.delayRange.upperBound),
                                step: 50)
                                .accessibilityLabel("Next after")
                            Text(String(format: "%.2f s", Double(model.queue.advanceDelayMilliseconds) / 1000))
                                .monospacedDigit().foregroundStyle(.secondary).fixedSize()
                        }
                        Text("Increase the delay if a slow app pastes the wrong snippet.")
                            .font(.caption).foregroundStyle(.secondary)
                    }
                    Divider()
                    section("Startup", icon: "power") {
                        Toggle("Start with Mac", isOn: Binding(
                            get: { loginStatus == .enabled || loginStatus == .requiresApproval },
                            set: { setLaunchAtLogin($0) }))
                            .toggleStyle(.switch)
                        Text("Open Clipboard Next automatically when you sign in.")
                            .font(.caption).foregroundStyle(.secondary)
                        if loginStatus == .requiresApproval {
                            Text("Allow Clipboard Next in System Settings > General > Login Items.")
                                .font(.caption).foregroundStyle(.orange)
                            Button("Open Login Items") { SMAppService.openSystemSettingsLoginItems() }
                        }
                        if loginStatus == .notFound {
                            Text("macOS could not find this app's login item. Try moving the app to Applications and reopening it.")
                                .font(.caption).foregroundStyle(.orange)
                        }
                        if let loginError {
                            Text(loginError).font(.caption).foregroundStyle(.red).textSelection(.enabled)
                        }
                    }
                    Divider()
                    section("About", icon: "info.circle") {
                        Text("Clipboard Next \(Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "Version unavailable")")
                            .font(.subheadline.weight(.medium))
                        HStack(spacing: 16) {
                            Link("Website", destination: URL(string: "https://clipboardnext.com/")!)
                            Link("Privacy", destination: URL(string: "https://clipboardnext.com/privacy.html")!)
                        }
                        Text("Local-only. No accounts, telemetry, or cloud sync.")
                            .font(.caption).foregroundStyle(.secondary)
                    }
                }
                .padding(20)
                .frame(maxWidth: .infinity, alignment: .leading)
            }
        }
        .frame(width: inMenu ? 380 : 420, height: inMenu ? 560 : 600)
        .onAppear { loginStatus = SMAppService.mainApp.status }
        .onChange(of: scenePhase) { _, phase in
            if phase == .active { loginStatus = SMAppService.mainApp.status }
        }
    }

    private func section<Content: View>(_ title: String, icon: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            Label(title, systemImage: icon).font(.headline)
            content()
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func setLaunchAtLogin(_ enabled: Bool) {
        do {
            if enabled { try SMAppService.mainApp.register() }
            else { try SMAppService.mainApp.unregister() }
            loginError = nil
        } catch {
            loginError = "Could not change Start with Mac: \(error.localizedDescription)"
        }
        loginStatus = SMAppService.mainApp.status
    }
}
