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

Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -and ($_.Path -ieq $scr) } | Stop-Process -Force -ErrorAction SilentlyContinue

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
    foreach ($dir in @((Join-Path $env:APPDATA "IslamicScreensaver"), (Join-Path $env:LOCALAPPDATA "IslamicScreensaver"))) {
        if (Test-Path $dir) { Remove-Item -Recurse -Force $dir; Write-Host "Removed $dir" }
    }
}
