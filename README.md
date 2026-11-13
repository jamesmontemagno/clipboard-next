# Clipboard Next

A small, local-only Swift macOS menu bar app for demo presenters. Prepare commands or prompts, press Start, and paste them one at a time without returning to your notes.

Website: <https://clipboardnext.com/> · A Windows version is in progress: see [Windows](#windows).

## Install

```sh
brew tap jamesmontemagno/clipboard-next https://github.com/jamesmontemagno/clipboard-next
brew trust jamesmontemagno/clipboard-next
brew install --cask clipboard-next
```

Or download the notarized zip from [GitHub Releases](https://github.com/jamesmontemagno/clipboard-next/releases/latest).

## Run from source

Requires macOS 14+ and Xcode 26+.

The app icon is maintained in [`ClipboardNext/AppIcon.icon`](ClipboardNext/AppIcon.icon) with Icon Composer. Xcode generates the icon for older supported macOS releases at build time.

Open `ClipboardNext.xcodeproj`, choose the **ClipboardNext** scheme and **My Mac**, and Run. No external packages are needed. The app target signs with the `Apple Development` identity for team `8P95D2238B`, so Input Monitoring approval survives rebuilds. Contributors without that team can build ad hoc by appending `CODE_SIGN_IDENTITY=- DEVELOPMENT_TEAM=` to `xcodebuild`.

Or build and launch from this directory:

```sh
xcodebuild -project ClipboardNext.xcodeproj -scheme ClipboardNext \
  -configuration Release -destination 'generic/platform=macOS' \
  -derivedDataPath build build
open build/Build/Products/Release/ClipboardNext.app
```

The Release build supports Apple Silicon and Intel. Find the clipboard icon in the menu bar; **Open Queue Editor** reopens the window. Closing the window keeps the app running. Use **Quit Clipboard Next** to exit.

## Prepare a demo

1. Add a snippet and enter its text, or choose **Import Markdown**. Snippets are numbered; there are no titles to manage.
2. Paste your presenter notes or open a UTF-8 Markdown file, then press **Preview**.
3. Review the candidates in source order. Fenced code blocks are selected by default; blockquoted prompts (`>`) are unchecked. Headings and language labels provide context but are not copied.
4. By default, each nonblank code line becomes one snippet. Enable **Keep each code block as one multiline snippet** for multiline commands, scripts, or shell continuations. Splitting is not shell-aware.
5. Append selected candidates, or confirm replacing the queue. Edit, delete, or move snippets up/down before starting.

For the TheaterLog script, select the commands needed for your chosen demo path, leaving troubleshooting commands unselected unless needed. Optional Copilot prompts appear separately. PowerShell commands remain PowerShell commands; import never converts shell syntax.

## Present

- **Start** copies the selected snippet (or the first) and arms the queue.
- Press **Command-V** in your destination app. After releasing V and waiting the **Next after** delay, the next snippet is copied. Adjust the delay with the slider in the editor (0.10–3.00 s, default 0.35 s); raise it if a slow app pastes the wrong snippet. The setting is remembered.
- The menu bar icon briefly becomes a checkmark when a paste gesture is detected.
- Enable **Show Floating Badge** for a tiny, always-on-top `3/12` capsule. Drag it anywhere on any display; **Reset Badge Position** in the menu puts it back in the top-right corner. If its saved spot ends up off screen (for example, after unplugging a monitor), it returns there automatically. Its clipboard icon flashes to a green checkmark on detection, without taking keyboard focus. Visibility and position are remembered.
- **Pause** cancels pending advancement. **Resume** copies the current snippet again.
- **Next** skips to and copies the next snippet without pasting. When paused, it stays paused. Before starting, it copies the first snippet.
- **Back** puts the previous snippet back on the clipboard, for example to redo a step. It cancels a paste that's still waiting to advance, and stepping back from a finished queue resumes it.
- **Start Here** restarts from the selected snippet. Pause first to edit the queue.
- The last paste marks the queue **Finished**, leaving the final snippet on the clipboard. Start again to replay.

If another app changes the clipboard, the queue pauses and preserves that content. Resume explicitly to copy the current queued snippet again. The clipboard is not cleared or restored on pause, quit, or launch.

## Permission and limitations

Choose **Enable Paste Detection** and allow **Clipboard Next** in **System Settings > Privacy & Security > Input Monitoring**. The Enable action opens that settings pane every time, including on retries. The app uses a listen-only keyboard event tap, never intercepts keystrokes, and does not request permission until you ask. macOS may require quitting and reopening it after changing permission. Manual Next works without permission.

Debug and Release have separate app identities and require separate approvals. For demos, consistently use the Release app from the build command above. **Reveal This App** in the editor locates the running copy, so you can add the correct one in System Settings.

Local builds use a stable Apple Development signature, so a grant persists across rebuilds. Ad-hoc builds (`CODE_SIGN_IDENTITY=-`) get a new signature on every rebuild, which invalidates the grant even if Settings still shows the toggle enabled. If that happens, quit the app, remove its stale Input Monitoring entry with the minus button, add the current app with the plus button, enable it, and reopen that same copy. No permissions are reset automatically.

Automatic detection observes an unmodified Command-V gesture on the physical V key (standard US layout). It ignores key repeats and pastes inside Clipboard Next. Shift/Option/Control variants, menu-based Paste, remote paste, alternate keyboard layouts/shortcuts, and applications using Secure Keyboard Entry may not be detected. Use Next manually for those workflows.

**A detected gesture is not proof of a successful paste.** macOS offers no universal successful-paste notification. Some apps read the clipboard asynchronously; the delay reduces races but cannot guarantee correctness in every app. Leave a beat between pastes: rapid repeated pastes during the delay may repeat the current snippet. Test your terminal/editor before presenting.

All commands are copied as plain text, never executed, and the app never presses Return. Multiline text can still execute when pasted into some terminals: review imported content before use.

## Storage

The queue is saved atomically to:

```text
~/Library/Application Support/ClipboardNext/queue.json
```

It restores without arming the queue or changing the clipboard. Save/load errors are displayed in the editor. A failed load never overwrites the original until you explicitly confirm **Save Current Queue**. Queue text is stored unencrypted on your Mac; avoid storing credentials or secrets. There are no network services, telemetry, or cloud sync.

## Releases

Releases follow the tiny-clips pattern. Pushing a tag like `v1.0.0-mac` (or running **Release** via workflow dispatch) triggers [`.github/workflows/release.yml`](.github/workflows/release.yml). It runs the tests, archives a universal build, signs it with Developer ID, notarizes and staples it, and publishes `ClipboardNext-v1.0.0-mac.zip` to GitHub Releases. It then opens a PR that updates the version and SHA-256 in [`Casks/clipboard-next.rb`](Casks/clipboard-next.rb). This repo is its own Homebrew tap. Merge that PR before announcing; until the first release, the cask's SHA is a placeholder.

Required repository secrets: `APPLE_MACOS_DIRECT_CERTIFICATE_P12` (base64-encoded Developer ID Application `.p12`), `APPLE_MACOS_DIRECT_CERTIFICATE_PASSWORD`, `APPLE_REFRACTORED_KEY_ID`, `APPLE_REFRACTORED_ISSUER_ID`, and `APPLE_REFRACTORED_P8_KEY` (the App Store Connect API private key). The workflow generates a temporary keychain password; the Apple team ID is already configured in the Xcode project. The installer certificate secrets are not needed because the release is a ZIP containing the signed app, not a `.pkg`. Allow GitHub Actions to create pull requests in repository settings.

[`.github/workflows/build.yml`](.github/workflows/build.yml) builds and tests every push and PR that touches the macOS app (ad-hoc signed, no secrets).

## Windows

A native Windows port lives in [`windows/`](windows/): a WinUI 3 app in the taskbar's notification area, with the same queue, Markdown import, paste detection (Ctrl+V), and floating badge. It is a work in progress and will be distributed only through the Microsoft Store.

- [`windows/README.md`](windows/README.md): how it works, how it differs from the macOS app, and how to build, run, and test it.
- [`windows/docs/microsoft-store-listing.md`](windows/docs/microsoft-store-listing.md): the Store listing and the submission and release process.

The queue logic is a direct port of the Swift code, and `queue.json` has the same format on both platforms. Its tests run on any OS:

```sh
dotnet test windows/tests/ClipboardNext.Core.Tests/ClipboardNext.Core.Tests.csproj
```

[`.github/workflows/windows-build.yml`](.github/workflows/windows-build.yml) tests and packages the Windows app on pushes and PRs that touch `windows/`. Windows releases use their own tags (`v1.0.0-windows`), which run [`.github/workflows/windows-store-publish.yml`](.github/workflows/windows-store-publish.yml): the Store upload goes to an Internal Testing flight first, and to the public listing only after a second approval.

## Website

The static site lives in [`site/`](site/) and deploys through [`.github/workflows/deploy-pages.yml`](.github/workflows/deploy-pages.yml) on pushes to `main` that touch `site/`. Enable it once in **Settings → Pages → Source: GitHub Actions**, then set **Custom domain** to `clipboardnext.com` and turn on **Enforce HTTPS**. Workflow-based deploys ignore `site/CNAME`, so the Settings field is what binds the domain; the file is only a reference. Preview locally with `cd site && python3 -m http.server 8765`. The Windows download card is a "Coming soon" placeholder until the Store listing is live. [`site/privacy.html`](site/privacy.html) is the privacy policy the Microsoft Store listing links to; keep it in step with what the apps actually do.

## Tests

```sh
xcodebuild -project ClipboardNext.xcodeproj -scheme ClipboardNext \
  -destination 'platform=macOS,arch=arm64' -derivedDataPath build test
```

On Intel, use `arch=x86_64`. Tests cover Markdown import and queue behavior using a fake clipboard and deterministic deferred callbacks, including repeated keys, canceled work, external copies, final-item behavior, and persistence protection.

For a live permission-dependent check, add three harmless snippets, start, and paste into a blank text document (not a shell). Confirm the first snippet appears before the next becomes available, the optional badge flashes without stealing focus, the third paste finishes, and copying unrelated text pauses the queue. This interactive check requires granting Input Monitoring on your Mac.

## License

[MIT](LICENSE) © James Montemagno
