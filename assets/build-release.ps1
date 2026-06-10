# SoundDeck - Release builder (Velopack)
# Usage:  powershell -ExecutionPolicy Bypass -File assets\build-release.ps1 -Version 1.0.1
#
# One-time setup (install the Velopack CLI, matching the library version):
#   dotnet tool install -g vpk --version 1.2.0
#
# Output: a "Releases" folder containing Setup.exe + the update packages.
# Upload the whole "Releases" folder to a GitHub Release tagged v<Version>.

param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

$root        = Split-Path $PSScriptRoot -Parent
$proj        = Join-Path $root "AudioDeviceTrayApp\AudioDeviceTrayApp.csproj"
$publishDir  = Join-Path $root "publish_temp"
$releasesDir = Join-Path $root "Releases"
$icon        = Join-Path $root "AudioDeviceTrayApp\app.ico"

Write-Host "==> Cleaning previous publish output" -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

Write-Host "==> Publishing SoundDeck $Version (self-contained, win-x64)" -ForegroundColor Cyan
dotnet publish $proj -c Release -r win-x64 --self-contained true -o $publishDir /p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "==> Packing with Velopack" -ForegroundColor Cyan
vpk pack `
    --packId SoundDeck `
    --packVersion $Version `
    --packDir $publishDir `
    --mainExe SoundDeck.exe `
    --packTitle "SoundDeck" `
    --icon $icon `
    --outputDir $releasesDir
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed" }

Write-Host ""
Write-Host "==> Done! Artifacts are in: $releasesDir" -ForegroundColor Green
Write-Host "    Next: create a GitHub Release tagged v$Version and upload everything in that folder." -ForegroundColor Yellow
Write-Host "    (Or push directly with: vpk upload github --repoUrl $($null) ... --token <PAT>)" -ForegroundColor DarkGray
