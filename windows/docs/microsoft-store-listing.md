# Clipboard Next for Windows - Microsoft Store Listing

The source of truth for the Microsoft Store listing and the first submission of the Windows app. Clipboard Next for Windows is distributed **only** through the Microsoft Store.

## App identity

| Field | Value |
| --- | --- |
| Product name | Clipboard Next |
| Package identity name | `23875RefractoredLLC.ClipboardNext` *(expected; confirm below)* |
| Publisher | `CN=995C4AD9-3B22-4B61-B30F-3EAB23CDAEAE` |
| Publisher display name | Refractored LLC |
| Primary category | Developer tools |
| Secondary category | Productivity |
| Pricing | Free, no in-app purchases |
| Privacy policy | https://clipboardnext.com/privacy.html |
| Website | https://clipboardnext.com/ |
| Support URL | https://github.com/jamesmontemagno/clipboard-next/issues |
| Copyright | Copyright (c) 2026 Refractored LLC |

> **Confirm the identity before the first build you submit.** The values in
> `windows/src/ClipboardNext.App/Package.appxmanifest` were written before the name was reserved:
> the publisher is the Refractored LLC account's, and the identity name follows the pattern
> Partner Center used for Tiny Clips. After reserving **Clipboard Next**, open
> **Product management → Product identity** and copy `Package/Identity/Name`,
> `Package/Identity/Publisher`, and `Package/Properties/PublisherDisplayName` into the manifest if
> they differ. A package whose identity does not match is rejected at upload.

## en-US listing copy

### Short description

Queue your demo commands and prompts, then paste them one at a time without going back to your notes.

### Description

Clipboard Next is a small tray app for demo presenters. Line up the commands and prompts for your talk, press Start, and just paste: after each paste, the next snippet is already on your clipboard.

Prepare your demo:

- Add snippets by hand, or import your Markdown presenter notes
- Fenced code blocks become snippets; blockquoted prompts are optional
- Reorder, edit, and delete before you go on stage

Present without looking away:

- Each Ctrl+V copies the next snippet, after a delay you control
- Optional Ctrl+Shift+V and Shift+Insert detection, for terminals
- A tiny always-on-top badge shows where you are, like 3/12, and flashes when a paste is detected
- Back, Next, Pause, and Start here, from the tray or the editor
- If another app changes the clipboard, the queue pauses instead of overwriting it

Private by design:

- Your queue stays on your device
- No account, no network connections, no telemetry
- Commands are copied as plain text and never run; the app never presses Enter

Clipboard Next is free and open source.

### Product features

- Queue commands and prompts and paste them in order
- Import snippets from Markdown presenter notes
- Automatic advance on Ctrl+V, with an adjustable delay
- Floating always-on-top progress badge
- Pauses when another app changes the clipboard
- Runs from the notification area; optional start with Windows
- Local only: no account, network, or telemetry

### Search terms

demo, presenter, clipboard, clipboard queue, paste, live coding, snippets

### What's new in this version

Copy the user-visible changes for the release. For the first release: "Clipboard Next comes to Windows."

## Screenshot set

Capture from the released Windows package, at 1920 x 1080 if possible (at least 1366 x 768), with no development tools, desktop clutter, or personal content visible. Use harmless sample commands. Use the same theme throughout the set.

| File | Content | Notes |
| --- | --- | --- |
| `01-editor.png` | The editor with a realistic queue and one snippet selected | The hero shot. |
| `02-tray-popup.png` | The tray popup while a queue is running | Show Current, Up next, and the controls. |
| `03-presenting.png` | A terminal mid-demo with the floating badge | Show the badge reading something like `3/12`. |
| `04-import-markdown.png` | The Markdown import page with a preview | Show code blocks selected and a prompt unselected. |
| `05-settings.png` | The Settings flyout | Show paste detection and the delay. |

Keep working files in `windows/docs/store-assets/` and do not commit screenshots containing personal data.

## Store assets

The package logos are generated into `windows/src/ClipboardNext.App/Assets/` by `windows/tools/generate-icons.py`. Upload the Store-logo variants Partner Center asks for and check them on light and dark backgrounds.

## Compliance answers

### Privacy

- Data collection: none. No account, advertising ID, analytics, or telemetry. The app makes no network connections.
- The queue and preferences are stored only on the device, in the app's own storage, and are removed on uninstall.
- Keyboard: paste detection is optional and off until the user turns it on. It recognizes the paste shortcut and nothing else; no keystroke is recorded, stored, transmitted, changed, or blocked.
- Clipboard: the app writes the user's own queued text to the clipboard. It reads the clipboard's text only to compare it with the current snippet, in memory.

Recheck these statements whenever network, account, analytics, or crash-reporting behavior changes, and keep https://clipboardnext.com/privacy.html in step.

### Capability justification

**runFullTrust:** Clipboard Next is a packaged desktop (WinUI 3) app. Full trust is required to show an icon in the notification area, to place the user's queued text on the clipboard, and, only after the user turns on paste detection, to install a listen-only low-level keyboard hook that recognizes the paste shortcut (Ctrl+V, and Ctrl+Shift+V and Shift+Insert if the user opts in) so the next queued snippet can be copied. The hook examines keys only while a queue is running, never blocks or modifies input, and nothing typed is recorded or leaves the device.

No other capabilities are declared.

### Notes for certification

- The app lives in the notification area (system tray). A normal launch also opens the editor window; closing that window leaves the app running in the tray. Use the tray icon's popup and its Exit button to quit.
- No account or sign-in is needed, and nothing is purchased in the app.
- To test: in the editor choose **Add** and type a short line of text, add two more, choose **Turn on paste detection**, then **Start**. Paste (Ctrl+V) into Notepad three times: each paste produces the next snippet. **Next** and **Back** work without paste detection.
- Paste detection uses a listen-only keyboard hook, described above and in the privacy policy. It is off by default.
- "Start with Windows" is off by default and is controlled by the user in Settings.

### Age rating

Complete the IARC questionnaire truthfully. The app has no user-generated public content, advertising, purchases, gambling, mature content, or web browsing.

## First submission and CI/CD setup

1. **Reserve the name** *Clipboard Next* in Partner Center and confirm the identity as described above.
2. **Verify on Windows.** Build and run the app on a Windows 11 machine and walk through the checks in [`windows/README.md`](../README.md#what-still-needs-a-windows-machine).
3. **Produce the first upload.** Push the release tag (for example `v1.0.0-windows`). The **Windows Store Publish** workflow's package job builds and verifies the upload. Its later jobs cannot submit anything yet: without the secrets from step 5 they stop at their configuration check. Download the `windows-store-package` artifact from that run. Or build the upload locally as shown in [`windows/packaging/README.md`](../packaging/README.md).
4. **Submit the first version by hand** in Partner Center: complete the listing, privacy, capability, age-rating, and pricing sections, upload the `.msixupload`, and submit for certification. The Microsoft Store Developer CLI can only update a product that is already live.
5. **Connect the workflow** once the product is live:
   - Create a Microsoft Entra application, add it under Partner Center **Account settings → User management → Microsoft Entra applications** with the **Manager** role, and create a client secret.
   - Create the GitHub environment `microsoft-store`, protect it with required reviewers, and add the environment secrets `PARTNER_CENTER_TENANT_ID`, `PARTNER_CENTER_SELLER_ID`, `PARTNER_CENTER_CLIENT_ID`, and `PARTNER_CENTER_CLIENT_SECRET`.
   - Add the repository variable `MICROSOFT_STORE_PRODUCT_ID`: the Store product ID (`msstore apps list` shows it), not the package identity or package family name.
   - Add the repository variable `MICROSOFT_STORE_FLIGHT_GROUP_IDS`: comma-separated Partner Center tester group IDs. It is used only if the workflow has to create the `Internal Testing` flight; an existing flight and its testers are left alone.
   - Run the workflow with **diagnose_only** checked to confirm the credentials work.
6. **Release from then on** by pushing a tag like `v1.0.1-windows`. The workflow packages the upload, pauses for approval to submit it to `Internal Testing`, and requires a second approval before submitting that same verified upload to the public listing. Between the two approvals, install the flight as a tester and run the release checks. Submission is not certification: the Store still reviews each update.

Tags have three parts (`v1.2.3-windows`) and become package version `1.2.3.0`; the Store reserves the fourth part. Each release needs a higher version than the last. Windows tags are independent of the macOS `v*-mac` tags.
