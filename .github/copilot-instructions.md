# Clipboard Next: Copilot instructions

Clipboard Next is a presenter tool. It queues demo snippets and puts the next one on the clipboard after each paste. There are two native apps that share one queue design.

| | macOS | Windows |
| --- | --- | --- |
| Code | `ClipboardNext/`, `ClipboardNextTests/`, `ClipboardNext.xcodeproj` | `windows/` |
| Stack | SwiftUI menu bar app, Swift 6, no external packages | WinUI 3 packaged app, .NET 10, C# |
| Tests | XCTest | xUnit on `ClipboardNext.Core` (runs on any OS) |
| Ships through | GitHub Releases plus the Homebrew cask in `Casks/` | Microsoft Store only |
| Release tag | `vX.Y.Z-mac` | `vX.Y.Z-windows` |

Platform-specific guidance lives in [`.github/instructions/macos.instructions.md`](instructions/macos.instructions.md) and [`.github/instructions/windows.instructions.md`](instructions/windows.instructions.md). The website is a static site in `site/`.

## Build and test

```sh
# macOS app tests
xcodebuild -project ClipboardNext.xcodeproj -scheme ClipboardNext \
  -destination 'platform=macOS,arch=arm64' -derivedDataPath build test

# Windows core tests (work on macOS, Linux, and Windows)
dotnet test windows/tests/ClipboardNext.Core.Tests/ClipboardNext.Core.Tests.csproj

# Website preview
cd site && python3 -m http.server 8765
```

CI runs `build.yml` for macOS and `windows-build.yml` for Windows changes, and both must pass before merging.

## Cross-platform rules

- **Keep behavior in step.** Queue rules, import parsing and the `queue.json` format are ported between Swift (`ClipboardNext/Queue.swift`, `MarkdownImport.swift`, `PlainTextImport.swift`) and C# (`windows/src/ClipboardNext.Core`). When you change one, change the other and add matching tests on both sides, or explain in the PR why the platforms intentionally differ.
- **Privacy is a product feature.** There are no network calls, accounts, telemetry, or cloud sync. Paste detection is opt-in and listen-only: it never blocks, changes, or records keystrokes, and only looks at the paste key while a queue is running. Never write queue text to logs. If you change what data the apps touch, update `site/privacy.html`.
- **Respect the clipboard.** Never clear or restore the user's clipboard on pause, quit, launch, or reset. If another app changes the clipboard, pause and preserve that content. Snippet titles are labels only and are never copied.
- **Never execute snippets.** Snippets are always copied as plain text, and the app never presses Return.
- **Tests are deterministic.** Use the fake clipboard and injected deferred callbacks instead of real timers, the real clipboard, or keyboard hooks.

## Docs and changelog

- Add user-visible changes to the `## [Unreleased]` section of [`CHANGELOG.md`](../CHANGELOG.md), and prefix each entry with **macOS**, **Windows**, or **macOS and Windows**. When releasing, move the entries under a new version heading.
- Update `README.md` (macOS) or `windows/README.md` when a feature, control label, or workflow changes. Name controls exactly as they appear in the UI.

## Releases

Versions come from tags; CI stamps them into the builds, so don't edit version numbers in project files.

- **macOS:** Pushing `vX.Y.Z-mac` runs `release.yml`. It tests, archives a universal build, signs it with Developer ID, notarizes it and publishes a ZIP to GitHub Releases. It then opens a `release-metadata/...` PR that updates `Casks/clipboard-next.rb`. Merge that PR before announcing the release.
- **Windows:** Pushing `vX.Y.Z-windows` runs `windows-store-publish.yml`. It packages the Store upload, then waits for an approval of the `microsoft-store` environment to submit to the Internal Testing flight, and a second approval to submit to production. Tags have three version parts; the Store reserves the fourth.
