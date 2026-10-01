<#
.SYNOPSIS
    Unregisters the screensaver and removes the installed files. Settings and downloaded recitations
    are left alone unless -Purge is given.

.DESCRIPTION
    Works for an install made by install.ps1 or by the setup.exe. Prefer Settings > Apps for a
    setup.exe install; this does the same job from a terminal.
#>
param(
    [switch]$Purge
)

$ErrorActionPreference = "Stop"

$installDir = Join-Path $env:LOCALAPPDATA "Programs\IslamicScreensaver"
$scr = Join-Path $installDir "IslamicScreensaver.scr"
$setupUninstaller = Join-Path $installDir "unins000.exe"
$desktopKey = "HKCU:\Control Panel\Desktop"

Get-CimInstance Win32_Process -Filter "Name = 'IslamicScreensaver.scr'" | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
Start-Sleep -Milliseconds 500

if ($Purge) {
    if (Test-Path $scr) {
        # The app does the purge itself: only it can read its settings to find a library moved to
        # another drive, and only it knows which folders are its own to delete. The setup's
        # uninstaller calls the same thing, so the two can never disagree about what is safe.
        # -NoNewWindow launches it directly; through the shell a .scr ignores its argument.
        $purge = Start-Process -FilePath $scr -ArgumentList "/purge" -NoNewWindow -Wait -PassThru
        if ($purge.ExitCode -eq 0) { Write-Host "Removed settings and every downloaded recitation." }
        else { Write-Host "Removed what it could; a file was in use. Close anything using the library and run this again." }
    } else {
        # Without the app there is no reading its settings, so a moved library cannot be found.
        foreach ($dir in @((Join-Path $env:APPDATA "IslamicScreensaver"), (Join-Path $env:LOCALAPPDATA "IslamicScreensaver"))) {
            if (Test-Path $dir) { Remove-Item -Recurse -Force $dir; Write-Host "Removed $dir" }
        }
        Write-Host "The app was already gone, so a library moved to another drive could not be located. Delete it by hand if you had one."
    }
}

if (Test-Path $setupUninstaller) {
    # Installed by the setup.exe: let its own uninstaller run, so the Settings > Apps entry goes too.
    # Silent, because the purge question has already been answered above.
    Start-Process -FilePath $setupUninstaller -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" -Wait
    Write-Host "Uninstalled."
    return
}

$current = (Get-ItemProperty -Path $desktopKey -Name "SCRNSAVE.EXE" -ErrorAction SilentlyContinue)."SCRNSAVE.EXE"
if ($current -and ($current -ieq $scr)) {
    Remove-ItemProperty -Path $desktopKey -Name "SCRNSAVE.EXE"
    Write-Host "Unregistered screensaver."
}

if (Test-Path $installDir) {
    Remove-Item -Recurse -Force $installDir
    Write-Host "Removed $installDir"
}
