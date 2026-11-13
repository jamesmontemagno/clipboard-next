# Clipboard Next for Windows

A native Windows port of [Clipboard Next](../README.md): a WinUI 3 app that lives in the taskbar's notification area (the system tray). Prepare commands or prompts, press Start, and paste them one at a time without returning to your notes.

It is distributed **only through the Microsoft Store**, as a packaged (MSIX) app. There is no direct download, sideload, or winget flavor: the Store signs the package and delivers updates.

> **Status: preparing for the Microsoft Store.** The native app runs on Windows, and the Store screenshot set is captured from the real WinUI app. Store certification and release validation are still required; see [What still needs a Windows machine](#what-still-needs-a-windows-machine).

## Using it

- **Tray icon.** Click it (either button) for the quick popup: status, the current and next snippet, **Back / Start · Pause · Resume / Next**, the floating badge toggle, **Open editor**, and **Exit**. A green dot on the icon marks a running queue, the tooltip shows progress such as `3 of 12`, and the icon flashes a checkmark when a paste is detected.
- **Editor.** Opens on a normal launch and from the tray. Add, edit, delete, and reorder snippets; **Start here** begins from the selected one; **Import Markdown** turns presenter notes into snippets. Closing the editor keeps the app running in the tray.
- **Paste detection** is off until you turn it on (in the editor's notice bar, the tray popup, or **Settings**). Then each **Ctrl+V** copies the next snippet after the **Next after** delay (0.10–3.00 s, default 0.35 s). Turn on **Terminal paste shortcuts** in Settings to also count **Ctrl+Shift+V** and **Shift+Insert**.
- **Floating badge.** An optional always-on-top `3/12` badge that never takes keyboard focus. Drag it anywhere on any display; **Reset position** puts it back in the top-right corner, and it returns there by itself if its display is unplugged.
- **Start with Windows** (Settings) starts the app quietly in the tray at sign-in.

If another app changes the clipboard, the queue pauses and leaves that content alone; **Resume** copies the current snippet again. Commands are copied as plain text, never executed, and the app never presses Enter.

## How it differs from the macOS app

| | macOS | Windows |
| --- | --- | --- |
| Home | Menu bar extra | Notification-area icon with a popup |
| Paste shortcut | Command-V | Ctrl+V, plus optional Ctrl+Shift+V and Shift+Insert |
| Detection | Listen-only event tap; needs the Input Monitoring permission | Listen-only low-level keyboard hook; no system permission, but off until you turn it on |
| Progress while running | Text in the menu bar | Tray tooltip, a dot on the tray icon, and the badge |
| Confirmations and settings | Dialogs and sheets | Flyouts; Markdown import is a page inside the editor |
| Line endings | LF | LF in the queue file; CR LF on the clipboard, as Windows apps expect |
| Storage | `~/Library/Application Support/ClipboardNext` | `%LOCALAPPDATA%\ClipboardNext` (redirected into the package's storage; removed on uninstall) |
| Distribution | Homebrew cask and GitHub Releases | Microsoft Store only |

`queue.json` has the same format on both platforms, so a queue file can be copied from one to the other.

### Paste detection on Windows

The keyboard hook never blocks, changes, or records a keystroke. It examines keys only while a queue is running, and then only V and Insert, to recognize a paste gesture. It ignores key repeats and pastes inside Clipboard Next.

- **Elevated windows.** Windows does not deliver keystrokes typed into a window running as administrator to a non-elevated app. Pastes into an elevated terminal are not detected; use **Next**.
- **Not a paste.** Win+V (clipboard history) and Ctrl+Alt+V (Paste Special) are ignored, as is AltGr+V. Right-click and menu Paste are not detected.
- **Ctrl+Shift+V** is the paste shortcut in some terminals but means something else in some apps (Markdown preview in VS Code's editor, for example). That is why **Terminal paste shortcuts** is off by default; like the macOS app, only the plain paste shortcut counts until you say otherwise.
- **A detected gesture is not proof of a successful paste.** Leave a beat between pastes, and test your terminal or editor before presenting.
- **Clipboard sync tools** (remote desktop, virtual machines, clipboard managers) can republish the same text. The queue only pauses when the clipboard's text actually differs from the current snippet.

## Requirements

- Windows 11 **21H2 (build 22000)** or later
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- **Developer Mode** enabled (Settings → System → For developers) to register the package locally
- [Windows App Development CLI (`winapp`)](https://learn.microsoft.com/windows/apps/dev-tools/winapp-cli/), which `dotnet run` uses to launch with package identity:
  ```powershell
  winget install Microsoft.winappcli --source winget
  ```

## Project layout

```
windows/
  ClipboardNext.Windows.slnx     Solution
  Directory.Build.props          Shared build settings
  src/
    ClipboardNext.Core/          UI-free logic: queue, Markdown import, settings, paste rules (plain .NET)
    ClipboardNext.App/           WinUI 3 packaged app: tray icon, popup, editor, badge, keyboard hook
  tests/
    ClipboardNext.Core.Tests/    xUnit tests, ported from the macOS tests (run on any OS)
  packaging/                     Store upload verification
  docs/                          Store listing and submission guide
  tools/generate-icons.py        Regenerates every icon in ClipboardNext.App/Assets
```

`ClipboardNext.Core` has no Windows dependencies, so its tests run anywhere, including on Linux in CI. Everything that touches Windows (the clipboard, the keyboard hook, windows, the tray) lives in `ClipboardNext.App`.

## Build, run, and test

WinUI 3 requires an explicit platform (`x64` or `ARM64`; `AnyCPU` is not supported). Run these from the repository root:

```powershell
# Restore
dotnet restore windows/ClipboardNext.Windows.slnx

# Build the app
dotnet build windows/src/ClipboardNext.App/ClipboardNext.App.csproj -c Debug -p:Platform=x64

# Run with package identity (uses the winapp CLI under the hood)
dotnet run --project windows/src/ClipboardNext.App/ClipboardNext.App.csproj -c Debug -p:Platform=x64

# Test (works on Windows, macOS, and Linux)
dotnet test windows/tests/ClipboardNext.Core.Tests/ClipboardNext.Core.Tests.csproj
```

Start the app through `dotnet run` or Visual Studio's **Package** profile rather than by double-clicking the built `.exe`: it is a packaged app and expects package identity.

The tests cover Markdown import and queue behavior with a fake clipboard and deterministic deferred callbacks (repeated keys, canceled work, external copies, final-item behavior, persistence protection, and reading the macOS file format), plus the Windows-only rules: which shortcuts count as a paste, badge placement across displays, and settings.

For a live check, add three harmless snippets, turn on paste detection, start, and paste into Notepad (not a shell). Confirm the first snippet appears before the next becomes available, the badge flashes without taking focus, the third paste finishes the queue, and copying unrelated text pauses it.

## Storage

The queue is saved atomically to `%LOCALAPPDATA%\ClipboardNext\queue.json`, with preferences beside it in `settings.json`. Because the app is packaged, Windows redirects that folder to `%LOCALAPPDATA%\Packages\<package family name>\LocalCache\Local\ClipboardNext` and removes it when the app is uninstalled.

Restoring never arms the queue or changes the clipboard. A queue file that fails to load is never overwritten until you confirm **Save current queue**. Queue text is stored unencrypted; avoid storing credentials or secrets. Unexpected errors are logged to `Logs\crash.log` in the same folder, with exception details only. There are no network services, telemetry, or cloud sync.

## Design notes

The tray behavior follows what [Tiny Clips for Windows](https://github.com/jamesmontemagno/tiny-clips/tree/main/windows) learned the hard way:

- **Single instance before XAML.** `Program.cs` replaces the generated entry point and hands a second launch to the running instance, which opens the editor.
- **Tray-first lifetime.** `DispatcherShutdownMode.OnExplicitShutdown` keeps the process alive without a window; only **Exit** ends it.
- **Guarded startup.** An exception escaping `OnLaunched` is fatal even when "handled", so every startup step is wrapped and logged.
- **Tray icon registration.** `ForceCreate(enablesEfficiencyMode: false)` (the default would throttle the whole process and distort paste timing), with retry and backoff when the shell has no taskbar yet.
- **Popup, not a context menu.** A borderless acrylic window shown at the cursor that light-dismisses, and closes its flyouts before hiding.
- **DPI.** Window sizes are kept in DIPs and converted per monitor; the badge re-measures when it lands on a display with a different scale.
- **No trimming or NativeAOT.** WinUI 3 and H.NotifyIcon rely on reflection.

Specific to this app:

- **The keyboard hook has its own thread.** Windows calls a low-level hook synchronously for every keystroke in every app, so it never waits on the UI thread.
- **The clipboard is written through Win32**, not a WinRT `DataPackage`, whose lazy rendering would move the clipboard sequence number after the write and look like another app taking the clipboard.
- **The badge is a non-activating tool window** (`WS_EX_NOACTIVATE`), so showing, clicking, or dragging it never takes focus from the app being demoed.

## Icons

Every file in `src/ClipboardNext.App/Assets` is generated from the macOS app icon's artwork:

```powershell
pip install pillow
python windows/tools/generate-icons.py
```

## CI and releases

- [`windows-build.yml`](../.github/workflows/windows-build.yml) runs on pushes and pull requests that touch `windows/`: the core tests on Linux, then an unsigned MSIX build for x64 and ARM64 on Windows.
- [`windows-store-publish.yml`](../.github/workflows/windows-store-publish.yml) runs for tags like `v1.0.0-windows`: it packages the Store upload, then waits for approval to submit it to the **Internal Testing** flight, and for a second approval to submit that same upload to the public listing.

See [`docs/microsoft-store-listing.md`](docs/microsoft-store-listing.md) for the listing copy, the first submission, and the one-time setup, and [`packaging/README.md`](packaging/README.md) for how the upload is built and verified.

- [Partner Center secret and variable setup](docs/partner-center-setup.md): the four `microsoft-store` environment secrets, two repository variables, portal locations, approvals, and safe diagnostics.
- [Three Store screenshots and capture instructions](docs/store-assets/README.md): real queue, Markdown import, and presenting views, prefilled with WinUI examples.

## What still needs a Windows machine

The port started on Linux and now runs on Windows. Before the first Store submission:

1. Let `windows-build.yml` compile and package the app, and fix whatever the XAML compiler reports.
2. Run the app and walk through the live check above, the tray popup, the badge on a second display, and both light and dark themes.
3. Reserve **Clipboard Next** in Partner Center and confirm the package identity in `Package.appxmanifest` matches.
4. Review the [three Store screenshots](docs/store-assets/README.md) against the release candidate and re-capture after any UI changes.
