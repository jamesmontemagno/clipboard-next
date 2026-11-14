# Changelog

All notable changes to Clipboard Next are recorded here. macOS and Windows ship separately, with their own release tags (`vX.Y.Z-mac` and `vX.Y.Z-windows`); entries note which platform each change applies to.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.1.0] - 2026-11-13

### Added

- **macOS and Windows:** The floating badge shows the current snippet's title next to the progress, such as `Seed demo data · 3/12`. Long titles are shortened with an ellipsis, and the full title appears on hover. Untitled snippets, and queues that haven't started, keep the compact progress-only badge.
- **macOS:** A Settings panel, opened with the gear in the menu bar dropdown or the Settings button in the editor. It has these sections:
  - Floating badge options.
  - Paste detection setup, including the **Next after** delay.
  - **Start with Mac**, which uses macOS Login Items.
  - About: the installed version and links to the website and privacy policy.
- **macOS:** A reset button in the menu bar dropdown returns the queue to Ready, without deleting snippets or changing the clipboard.
- **macOS:** A one-time welcome card in the menu bar dropdown explains paste detection, with **Enable Paste Detection** and **Not Now** buttons.

### Changed

- **macOS:** Redesigned the menu bar dropdown. It now has:
  - A progress bar.
  - **Current** and **Up next** cards that show snippet titles.
  - Full-width **Back**, **Start** and **Next** buttons.
  - A bottom row with Settings, **Open Editor**, a **Badge** toggle and a power button to quit.
- **macOS:** Moved paste detection controls and the advancement delay from the editor footer into Settings.

## [1.0.4] - 2026-11-13 (macOS) / [1.0.1] - 2026-11-13 (Windows)

### Added

- **macOS and Windows:** Choose **Markdown** or **Plain Text** when importing. In plain text, each nonblank line becomes a snippet. The app remembers the selected format.

## [1.0.0] - 2026-11-13 (Windows)

### Added

- **Windows:** First release, through the Microsoft Store. It's a WinUI 3 notification-area app with:
  - The same queue as the macOS app, with a compatible `queue.json` format.
  - Markdown import.
  - A Mica editor window.
  - A floating badge that never takes focus.
  - Opt-in Ctrl+V paste detection through a listen-only keyboard hook, with optional terminal paste shortcuts.
  - Optional start with Windows.

## [1.0.3] - 2026-11-12 (macOS)

### Fixed

- **macOS:** The editor's footer controls fit narrower windows.
- **macOS:** The Homebrew cask uses the non-deprecated macOS requirement syntax.

## [1.0.2] - 2026-11-12 (macOS)

### Added

- **macOS:** Added the Homebrew `brew trust` step to the install instructions.

## [1.0.1] - 2026-11-12 (macOS)

### Added

- **macOS:** A layered app icon, made with Icon Composer.

## [1.0.0] - 2026-11-12 (macOS)

### Added

- **macOS:** First release. Clipboard Next is a menu bar app that queues demo commands and puts the next one on the clipboard after each Command-V. It includes:
  - Markdown import.
  - A floating badge.
  - A Homebrew cask.
  - Signed and notarized universal builds.
  - The clipboardnext.com website.

[Unreleased]: https://github.com/jamesmontemagno/clipboard-next/compare/v1.1.0-mac...HEAD
[1.1.0]: https://github.com/jamesmontemagno/clipboard-next/compare/v1.0.4-mac...v1.1.0-mac
[1.0.4]: https://github.com/jamesmontemagno/clipboard-next/compare/v1.0.3-mac...v1.0.4-mac
[1.0.3]: https://github.com/jamesmontemagno/clipboard-next/compare/v1.0.2-mac...v1.0.3-mac
[1.0.2]: https://github.com/jamesmontemagno/clipboard-next/compare/v1.0.1-mac...v1.0.2-mac
[1.0.1]: https://github.com/jamesmontemagno/clipboard-next/compare/v1.0.0-mac...v1.0.1-mac
[1.0.0]: https://github.com/jamesmontemagno/clipboard-next/releases/tag/v1.0.0-mac
