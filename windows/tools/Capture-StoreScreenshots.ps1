#requires -Version 5.1
[CmdletBinding()]
param(
	[string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
	$OutputDirectory = Join-Path $PSScriptRoot '..\docs\store-assets'
}
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class StoreCaptureNative
{
	[StructLayout(LayoutKind.Sequential)]
	public struct Rect { public int Left, Top, Right, Bottom; }
	[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
	[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
	[DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr window, int x, int y, int width, int height, bool repaint);
	[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
	[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
}
'@
[StoreCaptureNative]::SetProcessDPIAware() | Out-Null

if (Get-Process ClipboardNext.App -ErrorAction SilentlyContinue) {
	throw 'Exit Clipboard Next from its tray popup before capturing. Your running queue will not be interrupted.'
}
$package = Get-AppxPackage -Name '23875RefractoredLLC.ClipboardNext'
if (-not $package) { throw 'Register/build the Windows package in Visual Studio before capturing.' }
$assets = (Resolve-Path (Join-Path $PSScriptRoot '..\docs\store-assets')).Path
$storage = Join-Path $env:LOCALAPPDATA "Packages\$($package.PackageFamilyName)\LocalCache\Local\ClipboardNext"
$backup = Join-Path ([IO.Path]::GetTempPath()) ("ClipboardNext-StoreCapture-" + [guid]::NewGuid())
New-Item -ItemType Directory -Force -Path $storage, $backup, $OutputDirectory | Out-Null
$saved = @{}
foreach ($name in @('queue.json', 'settings.json')) {
	$path = Join-Path $storage $name
	$saved[$name] = Test-Path $path
	if ($saved[$name]) { Copy-Item -LiteralPath $path -Destination (Join-Path $backup $name) }
}

function Find-Control([string]$Id) {
	$condition = [System.Windows.Automation.PropertyCondition]::new(
		[System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)
	$control = $script:editor.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
	if (-not $control) { throw "App control '$Id' was not found." }
	return $control
}

function Invoke-Control([string]$Id) {
	$control = Find-Control $Id
	$pattern = $control.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
	$pattern.Invoke()
	Start-Sleep -Milliseconds 500
}

function Get-WindowBitmap([System.Windows.Automation.AutomationElement]$Window) {
	$handle = [IntPtr]$Window.Current.NativeWindowHandle
	$rect = [StoreCaptureNative+Rect]::new()
	if (-not [StoreCaptureNative]::GetWindowRect($handle, [ref]$rect)) { throw 'Cannot measure capture window.' }
	$bitmap = [Drawing.Bitmap]::new($rect.Right - $rect.Left, $rect.Bottom - $rect.Top)
	$graphics = [Drawing.Graphics]::FromImage($bitmap)
	try {
		$dc = $graphics.GetHdc()
		try {
			if (-not [StoreCaptureNative]::PrintWindow($handle, $dc, 2)) { throw 'PrintWindow failed.' }
		}
		finally { $graphics.ReleaseHdc($dc) }
	}
	catch { $bitmap.Dispose(); throw }
	finally { $graphics.Dispose() }
	return $bitmap
}

function Save-Window([System.Windows.Automation.AutomationElement]$Window, [string]$Name,
	[System.Windows.Automation.AutomationElement[]]$Overlays = @()) {
	$bitmap = Get-WindowBitmap $Window
	$graphics = [Drawing.Graphics]::FromImage($bitmap)
	try {
		foreach ($overlay in $Overlays) {
			$image = Get-WindowBitmap $overlay
			try {
				$position = $overlay.Current.BoundingRectangle
				$origin = $Window.Current.BoundingRectangle
				$graphics.DrawImageUnscaled($image, [int]($position.X - $origin.X), [int]($position.Y - $origin.Y))
			}
			finally { $image.Dispose() }
		}
		$bitmap.Save((Join-Path $OutputDirectory $Name), [Drawing.Imaging.ImageFormat]::Png)
	}
	finally { $graphics.Dispose(); $bitmap.Dispose() }
}

function Find-AppWindowWithControl([string]$Id) {
	$condition = [System.Windows.Automation.PropertyCondition]::new(
		[System.Windows.Automation.AutomationElement]::ProcessIdProperty, $appProcess.Id)
	$windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
		[System.Windows.Automation.TreeScope]::Children, $condition)
	foreach ($window in $windows) {
		$control = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
			[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id))
		if ($control) { return $window }
	}
	throw "No app window contains '$Id'."
}

$appProcess = $null
try {
	Copy-Item -LiteralPath (Join-Path $assets 'demo-queue.json') -Destination (Join-Path $storage 'queue.json') -Force
	Copy-Item -LiteralPath (Join-Path $assets 'capture-settings.json') -Destination (Join-Path $storage 'settings.json') -Force
	Start-Process explorer.exe "shell:AppsFolder\$($package.PackageFamilyName)!App"
	for ($i = 0; $i -lt 40; $i++) {
		$appProcess = Get-Process ClipboardNext.App -ErrorAction SilentlyContinue
		if ($appProcess) {
			$condition = [System.Windows.Automation.AndCondition]::new(
				[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $appProcess.Id),
				[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, 'Clipboard Next'))
			$script:editor = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
				[System.Windows.Automation.TreeScope]::Children, $condition)
			if ($script:editor) { break }
		}
		Start-Sleep -Milliseconds 500
	}
	if (-not $script:editor) { throw 'The packaged app did not open its editor within 20 seconds.' }
	$handle = [IntPtr]$script:editor.Current.NativeWindowHandle
	if (-not [StoreCaptureNative]::MoveWindow($handle, 0, 0, 1600, 1000, $true)) { throw 'Cannot size the editor.' }
	[StoreCaptureNative]::SetForegroundWindow($handle) | Out-Null
	Start-Sleep -Seconds 2
	$items = (Find-Control 'SnippetList').FindAll([System.Windows.Automation.TreeScope]::Children,
		[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,
			[System.Windows.Automation.ControlType]::ListItem))
	if ($items.Count -ne 7) { throw "Expected seven demo snippets, found $($items.Count)." }
	$items[1].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
	Start-Sleep -Milliseconds 500
	Save-Window $script:editor '01-demo-queue.png'

	Invoke-Control 'ImportMarkdownButton'
	$notes = Get-Content -LiteralPath (Join-Path $assets 'presenter-notes.md') -Raw
	(Find-Control 'MarkdownBox').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($notes)
	(Find-Control 'WholeBlocksCheck').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
	Invoke-Control 'ImportPreviewButton'
	if ((Find-Control 'ImportSelectedCountText').Current.Name -notmatch '5') { throw 'Expected five selected code lines in the import preview.' }
	Save-Window $script:editor '02-markdown-import.png'

	Invoke-Control 'ImportCancelButton'
	$items[0].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
	Invoke-Control 'QueuePrimaryButton'
	Invoke-Control 'QueueNextButton'
	Invoke-Control 'QueueNextButton'
	$items[2].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
	$status = (Find-Control 'EditorStatusText').Current.Name
	if ($status -notmatch 'Running.*3 of 7') { throw "Expected the running queue at snippet three, got '$status'." }
	$badge = Find-AppWindowWithControl 'FloatingBadge'
	$badgeRect = $badge.Current.BoundingRectangle
	if (-not [StoreCaptureNative]::MoveWindow([IntPtr]$badge.Current.NativeWindowHandle, 1420, 100,
		[int]$badgeRect.Width, [int]$badgeRect.Height, $true)) { throw 'Cannot position the progress badge.' }

	Start-Sleep -Milliseconds 500
	Save-Window $script:editor '03-presenting.png' @($badge)
}
finally {
	if ($appProcess -and -not $appProcess.HasExited) {
		Stop-Process -Id $appProcess.Id
		$appProcess.WaitForExit()
	}
	foreach ($name in @('queue.json', 'settings.json')) {
		$path = Join-Path $storage $name
		if ($saved[$name]) {
			$original = Join-Path $backup $name
			Copy-Item -LiteralPath $original -Destination $path -Force
			if ((Get-FileHash -LiteralPath $original).Hash -ne (Get-FileHash -LiteralPath $path).Hash) {
				throw "Restore failed for $name. Recover the original from $backup."
			}
		}
		elseif (Test-Path $path) { Remove-Item -LiteralPath $path }
	}
	foreach ($name in @('queue.json', 'settings.json')) {
		$path = Join-Path $backup $name
		if (Test-Path $path) { Remove-Item -LiteralPath $path }
	}
	Remove-Item -LiteralPath $backup
}

foreach ($name in @('01-demo-queue.png', '02-markdown-import.png', '03-presenting.png')) {
	$path = Join-Path $OutputDirectory $name
	$image = [Drawing.Image]::FromFile($path)
	try {
		if ($image.Width -lt 1366 -or $image.Height -lt 768 -or (Get-Item $path).Length -gt 50MB) {
			throw "Screenshot $name does not meet the desktop Store requirements."
		}
		Write-Host "$name : $($image.Width) x $($image.Height)"
	}
	finally { $image.Dispose() }
}
Write-Host 'Captured three real app screenshots. Original queue and settings restored; clipboard contains harmless demo text.'