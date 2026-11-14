cask "clipboard-next" do
  # Updated automatically by .github/workflows/release.yml on each v*-mac tag.
  version "1.1.1"
  sha256 "431578043d15a887699752fc9e1a096332c8720e73a4cb560b7ace3f07d8742d"

  url "https://github.com/jamesmontemagno/clipboard-next/releases/download/v#{version}-mac/ClipboardNext-v#{version}-mac.zip"
  name "Clipboard Next"
  desc "Menu bar app that queues demo commands onto the clipboard, one paste at a time"
  homepage "https://clipboardnext.com/"

  depends_on macos: :sonoma

  app "ClipboardNext.app"

  uninstall quit: "com.refractored.clipboardnext"

  zap trash: [
    "~/Library/Application Support/ClipboardNext",
    "~/Library/Preferences/com.refractored.clipboardnext.plist",
  ]
end
