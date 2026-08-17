# SoundDeck - installs the locally signed test MSIX on this machine.
#
#   powershell -ExecutionPolicy Bypass -File packaging\install-test.ps1
#
# Trusting the test certificate needs administrator rights, so the script re-launches
# itself elevated (one UAC prompt). Uninstall again with:
#   Get-AppxPackage *SoundDeck* | Remove-AppxPackage

param(
    [string]$Msix,
    [string]$Cert
)

$ErrorActionPreference = "Stop"

$outDir = Join-Path $PSScriptRoot "out"
if (-not $Msix) {
    $Msix = (Get-ChildItem $outDir -Filter *.msix -ErrorAction SilentlyContinue |
             Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}
if (-not $Cert) { $Cert = Join-Path $outDir "SoundDeck-test-cert.cer" }

if (-not $Msix -or -not (Test-Path $Msix)) {
    throw "No .msix found in $outDir - run build-msix.ps1 -SelfSign first."
}

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
           ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "Elevating (installing a test certificate requires admin)..." -ForegroundColor Yellow
    Start-Process powershell -Verb RunAs -ArgumentList @(
        "-ExecutionPolicy", "Bypass", "-NoExit", "-File", "`"$PSCommandPath`"",
        "-Msix", "`"$Msix`"", "-Cert", "`"$Cert`""
    )
    return
}

if (Test-Path $Cert) {
    Write-Host "==> Trusting test certificate" -ForegroundColor Cyan
    Import-Certificate -FilePath $Cert -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
}

Write-Host "==> Installing $Msix" -ForegroundColor Cyan
Add-AppxPackage -Path $Msix

Write-Host ""
Write-Host "Installed. SoundDeck now appears in the Start menu." -ForegroundColor Green
Write-Host "Remove it again with: Get-AppxPackage *SoundDeck* | Remove-AppxPackage" -ForegroundColor DarkGray
