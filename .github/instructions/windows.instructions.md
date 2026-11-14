---
applyTo: "windows/**"
---

# Windows app

Clipboard Next for Windows is a WinUI 3 packaged app that lives in the notification area. It runs on .NET 10 and Windows 11 21H2 or later, and ships only through the Microsoft Store. See [`windows/README.md`](../../windows/README.md) for the full design notes.

## Layout

- **`windows/src/ClipboardNext.Core`:** Plain .NET code with no UI and no Windows dependencies. It holds the queue, snippet import, settings, paste rules, and badge placement and presentation.
- **`windows/src/ClipboardNext.App`:** The WinUI 3 app. It holds the tray icon and popup, the Mica editor, the badge window, the keyboard hook, clipboard access and the Store manifest.
- **`windows/tests/ClipboardNext.Core.Tests`:** xUnit tests for Core. They run on Microsoft.Testing.Platform, which is set in `global.json`.
- **`windows/packaging`, `windows/docs` and `windows/tools`:** Store upload verification, the Store listing and Partner Center docs, and the icon and screenshot generators.

## Build, run, and test

Run these commands from the repository root. WinUI 3 needs an explicit `x64` or `ARM64` platform; `AnyCPU` is not supported.

```powershell
dotnet restore windows/ClipboardNext.Windows.slnx
dotnet build windows/src/ClipboardNext.App/ClipboardNext.App.csproj -c Debug -p:Platform=x64
dotnet run --project windows/src/ClipboardNext.App/ClipboardNext.App.csproj -c Debug -p:Platform=x64
dotnet test windows/tests/ClipboardNext.Core.Tests/ClipboardNext.Core.Tests.csproj
```

- **Running the app:**
  - It needs Windows, Developer Mode and the `winapp` CLI (`winget install Microsoft.winappcli --source winget`).
  - Launch it with `dotnet run` or Visual Studio's **Package** profile. Don't double-click the built `.exe`, because the app needs package identity.
- **Testing on other OSes:**
  - The Core tests run on any OS, so use them to check queue and import changes even on macOS or Linux.
  - The App project only compiles on Windows. Elsewhere, rely on `windows-build.yml`, which builds unsigned x64 and ARM64 MSIX packages.

## Best practices

- **Put logic in Core,** and keep `ClipboardNext.App` a thin UI and platform layer. Add new rules, such as paste shortcuts, placement or presentation, to Core along with tests.
- **Mirror macOS.**
  - Core is a port of `ClipboardNext/Queue.swift`, `MarkdownImport.swift` and `PlainTextImport.swift`.
  - `queue.json` must stay readable on both platforms.
  - Change both platforms together and add matching tests.
- **Tray lifetime:**
  - `Program.cs` enforces a single instance before XAML loads.
  - `DispatcherShutdownMode.OnExplicitShutdown` keeps the app alive with no window open.
  - Wrap and log every startup step, because an exception that escapes `OnLaunched` is fatal.
  - Create the tray icon with `ForceCreate(enablesEfficiencyMode: false)`.
- **Keyboard hook:**
  - It runs on its own thread and never waits on the UI thread.
  - It only listens, and it inspects only V and Insert while a queue is running.
- **Clipboard:**
  - Write the clipboard through Win32, not a WinRT `DataPackage`.
  - Use CR LF line endings on the clipboard and LF in `queue.json`.
- **Badge:**
  - It is a non-activating tool window (`WS_EX_NOACTIVATE`) and must never take focus.
  - Keep sizes in DIPs, and re-measure when the DPI changes.
- **UI:**
  - Follow polished Windows 11 Fluent design: Mica or acrylic backgrounds, `SettingsCard` and `SettingsExpander`, and Fluent icons.
  - Prefer flyouts (`Flyout`, `MenuFlyout`, `TeachingTip`) to dialogs.
  - Support light and dark themes.
- **No trimming or NativeAOT,** because WinUI 3 and H.NotifyIcon rely on reflection.
- **Logs:** Write unexpected errors to `Logs\crash.log` with exception details only. Never log queue text.
- **Releases:**
  - Don't hand-edit the version in `Package.appxmanifest`; the publish workflow stamps it from the `vX.Y.Z-windows` tag.
  - Re-capture the Store screenshots in `windows/docs/store-assets` after visible UI changes.
- **After a change:** Run the Core tests, then update `windows/README.md` and the `[Unreleased]` section of `CHANGELOG.md`.
