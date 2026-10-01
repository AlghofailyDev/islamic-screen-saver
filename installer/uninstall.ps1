<#
.SYNOPSIS
    Unregisters the screensaver and removes the installed files. Settings and cache are left alone
    unless -Purge is given.
#>
param(
    [switch]$Purge
)

$ErrorActionPreference = "Stop"

$installDir = Join-Path $env:LOCALAPPDATA "Programs\IslamicScreensaver"
$scr = Join-Path $installDir "IslamicScreensaver.scr"
$desktopKey = "HKCU:\Control Panel\Desktop"

Get-CimInstance Win32_Process -Filter "Name = 'IslamicScreensaver.scr'" | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
Start-Sleep -Milliseconds 500

$current = (Get-ItemProperty -Path $desktopKey -Name "SCRNSAVE.EXE" -ErrorAction SilentlyContinue)."SCRNSAVE.EXE"
if ($current -and ($current -ieq $scr)) {
    Remove-ItemProperty -Path $desktopKey -Name "SCRNSAVE.EXE"
    Write-Host "Unregistered screensaver."
}

if (Test-Path $installDir) {
    Remove-Item -Recurse -Force $installDir
    Write-Host "Removed $installDir"
}

if ($Purge) {
    # A library moved to another drive lives outside AppData, and settings.json is the only record of
    # where. Read it before deleting the settings, or the downloads are orphaned somewhere unfindable.
    $settingsFile = Join-Path $env:APPDATA "IslamicScreensaver\settings.json"
    $movedLibrary = $null
    if (Test-Path $settingsFile) {
        try { $movedLibrary = (Get-Content $settingsFile -Raw -Encoding UTF8 | ConvertFrom-Json).CacheDirectory } catch { }
    }

    # The app only ever moves the library into "<chosen folder>\IslamicScreensaver\cache", never into a
    # folder the user picked directly, so that shape is required before anything is deleted. A
    # hand-edited path pointing somewhere else is reported and left alone.
    if ($movedLibrary) {
        $leaf = Split-Path $movedLibrary -Leaf
        $parent = Split-Path $movedLibrary -Parent
        if ($leaf -ieq "cache" -and (Split-Path $parent -Leaf) -ieq "IslamicScreensaver") {
            if (Test-Path $parent) { Remove-Item -Recurse -Force $parent; Write-Host "Removed moved library $parent" }
        } else {
            Write-Host "Left $movedLibrary alone: it is not a folder this app created. Delete it yourself if you no longer want it."
        }
    }

    foreach ($dir in @((Join-Path $env:APPDATA "IslamicScreensaver"), (Join-Path $env:LOCALAPPDATA "IslamicScreensaver"))) {
        if (Test-Path $dir) { Remove-Item -Recurse -Force $dir; Write-Host "Removed $dir" }
    }
}
