cask "clipboard-next" do
  # Updated automatically by .github/workflows/release.yml on each v*-mac tag.
  version "1.0.2"
  sha256 "2306198ab67034dfafce225dc29e09903d8b22c7d0197511fdc9bf8eca93d387"

  url "https://github.com/jamesmontemagno/clipboard-next/releases/download/v#{version}-mac/ClipboardNext-v#{version}-mac.zip"
  name "Clipboard Next"
  desc "Menu bar app that queues demo commands onto the clipboard, one paste at a time"
  homepage "https://clipboardnext.com/"

  depends_on macos: ">= :sonoma"

  app "ClipboardNext.app"

  uninstall quit: "com.refractored.clipboardnext"

  zap trash: [
    "~/Library/Application Support/ClipboardNext",
    "~/Library/Preferences/com.refractored.clipboardnext.plist",
  ]
end
