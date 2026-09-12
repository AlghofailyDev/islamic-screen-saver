<#
.SYNOPSIS
    Publishes the screensaver and registers it as the current user's screensaver.

.DESCRIPTION
    Installs per-user (no admin needed) to %LOCALAPPDATA%\Programs\IslamicScreensaver and points
    HKCU\Control Panel\Desktop\SCRNSAVE.EXE at the .scr. Windows' own idle timer then launches it.
    Re-run after every change; it overwrites in place.

.PARAMETER Configuration
    Build configuration to publish. Default: Release.

.PARAMETER SkipPublish
    Only (re)write the registry entries; do not rebuild.

.PARAMETER Seed
    After installing, download the starter content (one reciter, Al-Fatihah + Juz Amma, English
    translation) into %LOCALAPPDATA%\IslamicScreensaver\cache. Safe to re-run; already-cached items are skipped.
#>
param(
    [string]$Configuration = "Release",
    [switch]$SkipPublish,
    [switch]$Seed
)

$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "src\IslamicScreensaver\IslamicScreensaver.csproj"
$installDir = Join-Path $env:LOCALAPPDATA "Programs\IslamicScreensaver"
$scr = Join-Path $installDir "IslamicScreensaver.scr"

if (-not $SkipPublish) {
    # Running instances (fullscreen, settings, or the dialog's preview) hold the files open; stop them first.
    Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -and ($_.Path -ieq $scr) } | Stop-Process -Force -ErrorAction SilentlyContinue

    Write-Host "Publishing ($Configuration, self-contained win-x64) to $installDir ..."
    dotnet publish $project -c $Configuration -r win-x64 --self-contained true -o $installDir --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
}

if (-not (Test-Path $scr)) { throw "Expected $scr after publish, but it is missing." }

$desktopKey = "HKCU:\Control Panel\Desktop"
Set-ItemProperty -Path $desktopKey -Name "SCRNSAVE.EXE" -Value $scr
Set-ItemProperty -Path $desktopKey -Name "ScreenSaveActive" -Value "1"

# Windows caches screensaver settings; SPI_SETSCREENSAVEACTIVE with SPIF_SENDCHANGE makes it re-read them.
Add-Type -Namespace Native -Name User32 -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
'@
$SPI_SETSCREENSAVEACTIVE = 0x0011
$SPIF_UPDATEINIFILE = 0x01
$SPIF_SENDCHANGE = 0x02
[void][Native.User32]::SystemParametersInfo($SPI_SETSCREENSAVEACTIVE, 1, [IntPtr]::Zero, $SPIF_UPDATEINIFILE -bor $SPIF_SENDCHANGE)

if ($Seed) {
    Write-Host ""
    Write-Host "Downloading starter content..."
    $seedProcess = Start-Process -FilePath $scr -ArgumentList "/seed" -NoNewWindow -Wait -PassThru
    if ($seedProcess.ExitCode -ne 0) { throw "Starter content download failed (exit code $($seedProcess.ExitCode)). See %LOCALAPPDATA%\IslamicScreensaver\logs." }
}

$timeout = (Get-ItemProperty -Path $desktopKey -Name "ScreenSaveTimeOut" -ErrorAction SilentlyContinue).ScreenSaveTimeOut
Write-Host ""
Write-Host "Installed: $scr"
Write-Host "Registered as the current user's screensaver (idle timeout: $(if ($timeout) { "$timeout s" } else { 'Windows default' }))."
Write-Host "Open Settings > Personalization > Lock screen > Screen saver to confirm, or run:  & '$scr' /s"
