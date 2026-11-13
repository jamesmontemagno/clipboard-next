# Microsoft Store screenshot set

Three real WinUI 3 captures, **1600 x 1000 PNG**, using harmless WinUI demo snippets. Upload only `01-demo-queue.png`, `02-markdown-import.png`, and `03-presenting.png`, in that order. Captions and the complete listing are in [microsoft-store-listing.md](../microsoft-store-listing.md#screenshot-set).

## Re-capture

Build/register the desired package using Visual Studio's **Package** profile, and verify that it runs. Then **Exit Clipboard Next from its tray popup** (closing the editor is not enough). From the repository root, run on an interactive Windows desktop:

```powershell
powershell.exe -NoProfile -STA -File .\windows\tools\Capture-StoreScreenshots.ps1
```

No added Python packages or image editor are needed. The script uses Windows UI Automation and `PrintWindow`, not a screenshot of your entire desktop. It refuses to interrupt an already running app, backs up the existing queue and settings, loads the supplied fixtures, launches the registered package, and captures:

1. **Prepare:** seven titled WinUI examples with a multiline Fluent button selected.
2. **Import:** real Markdown preview, whole code blocks enabled, five code blocks selected and the assistant prompt left optional.
3. **Present:** the running queue at **3 of 7**, transport controls, and the real always-on-top **3/7** badge. The badge window is composited at its actual position over the editor capture; no UI is fabricated or marketing copy added.

The script stops only the app process it launches and restores the original `queue.json` and `settings.json`, including on failure. Restoration is hash-checked before the temporary backup is deleted. If restoration fails, the error gives the temporary backup location for recovery. The app is left closed; relaunch it to see your original queue. **The clipboard will contain harmless sample text** after the presenting capture. The fixtures enable paste detection only for the capture; original preferences are restored afterward. Do not type or use the clipboard while it is running.

The Windows app's normal first-run queue is **not** changed. `demo-queue.json`, `presenter-notes.md`, and `capture-settings.json` are screenshot fixtures, not production defaults. To use these examples in your own queue, open the editor's **Import Markdown** page and open `presenter-notes.md`; select **Keep each code block as one multiline snippet**, preview, then append. XAML and C# blocks must stay intact. The commands assume an existing WinUI 3 project named `DemoApp`; they are not an app scaffolder and are never executed by the capture script.

The script uses the current Windows app theme and English automation labels. Use the same theme as the checked-in set (light), a display with sufficient space for a 1600 x 1000 window, and an unlocked desktop. It validates expected queue/import state plus dimensions and file size, but does not replace checking the images for clipped content, blank GPU rendering, notifications, or other unexpected content. Inspect before uploading and re-capture against the actual release candidate after UI changes.

Microsoft's [MSIX image guidance](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/screenshots-and-images) requires PNG desktop screenshots of at least 1366 x 768 and no more than 50 MB each, and recommends no added marketing messages or logos. Put benefit copy in the caption fields. These files are **Desktop screenshots**, not optional Super hero artwork.