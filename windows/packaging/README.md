# Clipboard Next for Windows - Packaging and Distribution

Clipboard Next for Windows is a packaged (MSIX, identity-bearing) WinUI 3 app distributed **only through the Microsoft Store**. Package identity is what gives it a startup task, clean uninstall, and Store updates.

There is deliberately no direct-download, sideload, App Installer, or winget flavor:

- The Store signs the package, so the repository holds no code-signing certificate or signing step.
- The Store delivers updates, so the app has no update checker.
- The package is framework-dependent: it declares the shared Windows App Runtime, which the Store installs with the app, instead of bundling it.

## The Store upload

A Store submission is one `.msixupload` file containing an `.msixbundle` with the x64 and ARM64 packages, plus `.appxsym` crash symbols.

[`windows-store-publish.yml`](../../.github/workflows/windows-store-publish.yml) builds it for every `v*-windows` tag. To build it locally on Windows (the symbols need the Visual Studio C++ tools):

```pwsh
# From the repository root
$packageDir = (Resolve-Path .).Path + "\artifacts\store\"

dotnet build windows\src\ClipboardNext.App\ClipboardNext.App.csproj -c Release `
  -p:Platform=x64 `
  -p:SelfContained=false -p:WindowsAppSDKSelfContained=false `
  -p:PublishAot=false -p:PublishTrimmed=false `
  -p:EnableMsixTooling=true -p:GenerateAppxPackageOnBuild=true `
  -p:AppxPackageDir=$packageDir `
  -p:AppxBundle=Always -p:AppxBundlePlatforms="x64|ARM64" `
  -p:UapAppxPackageBuildMode=StoreUpload -p:AppxPackageSigningEnabled=false `
  -p:DebugSymbols=true -p:DebugType=portable -p:AppxSymbolPackageEnabled=true

.\windows\packaging\Assert-StoreUpload.ps1 -UploadPath (Get-ChildItem $packageDir -Recurse -Filter *.msixupload | Select-Object -First 1).FullName
```

Build and package in one MSBuild invocation, as above. Do not enable trimming or NativeAOT: WinUI 3 and H.NotifyIcon rely on reflection, and the failures only show at run time.

### Version

The package version comes from `Identity/@Version` in [`Package.appxmanifest`](../src/ClipboardNext.App/Package.appxmanifest). The workflow stamps it from the tag: `v1.2.3-windows` becomes `1.2.3.0`. The Store reserves the fourth part, so tags have three, and each submission must have a higher version than the last.

### Crash-report symbols

`Assert-StoreUpload.ps1` runs before the upload is saved as an artifact and again before each submission. It requires both the x64 and ARM64 application packages and `.appxsym` symbols that match the shipped `ClipboardNext.App.dll` and `ClipboardNext.Core.dll`, so Partner Center can symbolicate crash reports. A missing architecture or mismatched symbols stop publication. The script submits nothing, and can be run against a downloaded `windows-store-package` artifact.

Submit the complete `.msixupload`, not just its `.msixbundle`, to keep the symbols.

## Release pipeline

1. **Package** (every `v*-windows` tag, or a manual run): runs the tests, stamps the version, builds the upload, verifies it, and saves it as the `windows-store-package` artifact.
2. **Internal Testing** (after approval of the `microsoft-store` environment): submits that artifact to the `Internal Testing` package flight and records its SHA-256.
3. **Production** (after a second approval): checks the artifact still has that SHA-256, then submits it to the public listing.

Nothing reaches the public Store without the same upload first going to Internal Testing. A manual run can also package without publishing, or only check the Partner Center credentials (`diagnose_only`).

The one-time Partner Center and GitHub setup, including the secrets and variables the workflow needs, is in [`../docs/microsoft-store-listing.md`](../docs/microsoft-store-listing.md#first-submission-and-cicd-setup).

## Capabilities

The manifest declares only `runFullTrust`, required for a desktop app with a notification-area icon, direct clipboard access, and the optional listen-only keyboard hook used for paste detection. It declares one extension: a `windows.startupTask`, disabled until the user turns on **Start with Windows**.
