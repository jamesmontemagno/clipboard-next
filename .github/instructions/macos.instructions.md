---
applyTo: "ClipboardNext/**,ClipboardNextTests/**,ClipboardNext.xcodeproj/**,Casks/**"
---

# macOS app

Clipboard Next for macOS is a SwiftUI menu bar app. It needs macOS 14+, Xcode 26+ and Swift 6, and has no external packages.

## Layout

| File | Role |
| --- | --- |
| `ClipboardNextApp.swift` | The app entry point and `AppModel` (`@Observable`). Also the menu bar dropdown (`MenuView`), which shows the welcome card and Settings in place, and the shared `QueueControls`. |
| `EditorView.swift` | The queue editor window (`Window` id `editor`), including the import UI |
| `SettingsView.swift` | Settings. It appears inside the dropdown (`inMenu: true`) or as a popover from the editor's `SettingsButton`. |
| `MarkdownImport.swift`, `PlainTextImport.swift` | Import parsers |
| `Queue.swift` | `DemoQueue`: the queue state machine, persistence and advancement |
| `ClipboardMonitor.swift` | Clipboard writes and the listen-only Command-V event tap |
| `FloatingBadge.swift` | The non-activating floating badge panel and `BadgePresentation` |

## Build, run, and test

```sh
# Test (on Intel, use arch=x86_64)
xcodebuild -project ClipboardNext.xcodeproj -scheme ClipboardNext \
  -destination 'platform=macOS,arch=arm64' -derivedDataPath build test

# Build and launch a Release build
xcodebuild -project ClipboardNext.xcodeproj -scheme ClipboardNext \
  -configuration Release -destination 'generic/platform=macOS' \
  -derivedDataPath build build
open build/Build/Products/Release/ClipboardNext.app
```

- **Long output:** `xcodebuild` output is very long. Filter it for `error:`, `Executed`, and `TEST SUCCEEDED` or `TEST FAILED`.
- **Signing:** The app target signs with `Apple Development` for team `8P95D2238B`, so the Input Monitoring grant survives rebuilds. Without that team, build ad hoc by appending `CODE_SIGN_IDENTITY=- DEVELOPMENT_TEAM=`. Ad-hoc builds lose the Input Monitoring grant on every rebuild.
- **Debug identity:** The Debug build is a separate app identity (**Clipboard Next Debug**, `com.refractored.clipboardnext.debug`), so it needs its own Input Monitoring approval.
- **Relaunching:** A menu bar app keeps running after a rebuild. Quit the running copy before you open the new build.
- **Tests:** `AppModel` doesn't start the monitor under XCTest (it checks `XCTestConfigurationFilePath`), so tests never install an event tap.

## Best practices

- **State:** Use `@Observable` models annotated with `@MainActor`: `AppModel`, `DemoQueue`, `ClipboardMonitor` and `FloatingBadge`. Bind to them with `@Bindable`. Use `@AppStorage` for simple remembered UI flags.
- **Queue logic:** Put it in `DemoQueue`, not in views. Inject the clipboard (`ClipboardAccess`), the storage URL and deferred scheduling, so tests can use the fake clipboard with deterministic callbacks.
- **Mirror Windows:** `DemoQueue` and the importers are ported to `windows/src/ClipboardNext.Core`. Make behavior changes on both platforms and test both.
- **Paste detection:**
  - It uses `CGEvent.tapCreate(... options: .listenOnly)` and checks only the physical V key (key code 9) with Command and no other modifier.
  - Never request Input Monitoring until the user asks, through the welcome card or Settings.
  - Handle `tapDisabledByTimeout` and `tapDisabledByUserInput` by pausing the queue.
- **Focus:** The floating badge must never take keyboard focus from the app being presented.
- **UI:**
  - Use native SwiftUI controls and SF Symbols.
  - Add `.help(...)` tooltips, and give icon-only buttons an `accessibilityLabel`.
  - The dropdown is 380 points wide. Keep its bottom row in this order: Settings gear, **Open Editor**, a spacer, **Badge**, then the power button.
- **Settings:**
  - Start with Mac uses `SMAppService.mainApp`. Handle its `.requiresApproval` and `.notFound` states.
  - Read the version from `CFBundleShortVersionString`.
- **Persistence:** The queue saves atomically to `~/Library/Application Support/ClipboardNext/queue.json`. A queue file that fails to load is never overwritten until the user confirms.
- **After a change:** Run the tests, then update `README.md` and the `[Unreleased]` section of `CHANGELOG.md`.
- **Homebrew cask:** Don't edit `Casks/clipboard-next.rb` by hand. The release workflow opens a PR that updates it.
