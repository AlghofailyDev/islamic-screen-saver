<#
.SYNOPSIS
    Builds the setup.exe a tester can simply double-click.

.DESCRIPTION
    Publishes the app self-contained, then packages it with Inno Setup into
    installer\Output\IslamicScreensaver-Setup-<version>.exe. Attach that file to a GitHub release
    rather than committing it.

    Needs Inno Setup 6 on this machine only — the person installing needs nothing:
        winget install --id JRSoftware.InnoSetup --scope user

.PARAMETER Configuration
    Build configuration to publish. Default: Release.
#>
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "src\IslamicScreensaver\IslamicScreensaver.csproj"
$script = Join-Path $PSScriptRoot "IslamicScreensaver.iss"
$publish = Join-Path $root "publish\setup"

# 0.9.<commits>: every test build is newer than the last, so it upgrades in place, and a tester can
# say exactly which one they are running. Built from a ZIP with no history, it falls back to 0.9.0.
$commits = $null
try { $commits = (git -C $root rev-list --count HEAD 2>$null) } catch { }
$version = if ($commits) { "0.9.$($commits.Trim())" } else { "0.9.0" }

$iscc = @(
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup 6 is not installed. Install it with:  winget install --id JRSoftware.InnoSetup --scope user"
}

Write-Host "Publishing $version ($Configuration, self-contained win-x64) ..."
if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
# The version goes into the assembly too, so the log of every run says which build it was.
dotnet publish $project -c $Configuration -r win-x64 --self-contained true -o $publish --nologo -p:Version=$version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

Write-Host "Packaging with Inno Setup ..."
& $iscc /Q "/DAppVersion=$version" "/DSourceDir=$publish" $script
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE" }

$setup = Join-Path $PSScriptRoot "Output\IslamicScreensaver-Setup-$version.exe"
Write-Host ""
Write-Host "Built: $setup  ($([math]::Round((Get-Item $setup).Length / 1MB)) MB)"
