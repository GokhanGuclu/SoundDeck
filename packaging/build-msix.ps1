# SoundDeck - Microsoft Store (MSIX) package builder
#
# Usage:
#   # package for the Store (unsigned - the Store signs it for you)
#   powershell -ExecutionPolicy Bypass -File packaging\build-msix.ps1 -Version 1.0.4
#
#   # package signed with a throwaway certificate so you can install and test it locally
#   powershell -ExecutionPolicy Bypass -File packaging\build-msix.ps1 -Version 1.0.4 -SelfSign
#
# Identity values come from packaging\identity.json (copy identity.sample.json and fill it
# in with the values Partner Center shows under Product > Product identity), or from the
# -IdentityName / -Publisher / -PublisherDisplayName parameters.
#
# Output: packaging\out\SoundDeck-<version>-<arch>.msix

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$IdentityName,
    [string]$Publisher,
    [string]$PublisherDisplayName,

    [ValidateSet("x64", "arm64")]
    [string]$Arch = "x64",

    [switch]$SelfSign
)

$ErrorActionPreference = "Stop"

$root     = Split-Path $PSScriptRoot -Parent
$proj     = Join-Path $root "AudioDeviceTrayApp\AudioDeviceTrayApp.csproj"
$stage    = Join-Path $PSScriptRoot "staging\$Arch"
$outDir   = Join-Path $PSScriptRoot "out"
$toolsDir = Join-Path $PSScriptRoot "tools"
$images   = Join-Path $PSScriptRoot "Images"

# ---------------------------------------------------------------- identity ----

$idFile = Join-Path $PSScriptRoot "identity.json"
if (Test-Path $idFile) {
    # -Encoding UTF8 matters: Windows PowerShell otherwise reads the file as ANSI and
    # mangles non-ASCII publisher names.
    $id = Get-Content $idFile -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $IdentityName)         { $IdentityName = $id.IdentityName }
    if (-not $Publisher)            { $Publisher = $id.Publisher }
    if (-not $PublisherDisplayName) { $PublisherDisplayName = $id.PublisherDisplayName }
}

if (-not $IdentityName -or -not $Publisher -or -not $PublisherDisplayName) {
    throw "Missing identity values. Copy packaging\identity.sample.json to packaging\identity.json and fill in the values from Partner Center (Product > Product identity)."
}

# MSIX versions are 4-part and the Store reserves the last part, which must be 0.
$v = [Version]::Parse($Version)
$pkgVersion = "{0}.{1}.{2}.0" -f $v.Major, $v.Minor, [Math]::Max($v.Build, 0)
$asmVersion = "{0}.{1}.{2}" -f $v.Major, $v.Minor, [Math]::Max($v.Build, 0)

# The displayed product name lives in AppInfo.cs so the app and the manifest can never
# disagree - the Store rejects a package whose name is not the reserved one.
$appInfoPath = Join-Path $root "AudioDeviceTrayApp\AppInfo.cs"
$m = [regex]::Match((Get-Content $appInfoPath -Raw), 'StoreDisplayName\s*=\s*"([^"]+)"')
if (-not $m.Success) { throw "Could not read StoreDisplayName from $appInfoPath" }
$displayName = $m.Groups[1].Value

Write-Host "==> $displayName $pkgVersion ($Arch)" -ForegroundColor Cyan
Write-Host "    Identity : $IdentityName"
Write-Host "    Publisher: $Publisher"

# ------------------------------------------------------------------ publish ----

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage, $outDir | Out-Null

Write-Host "==> Publishing (self-contained, win-$Arch, StoreBuild)" -ForegroundColor Cyan
dotnet publish $proj -c Release -r "win-$Arch" --self-contained true -o $stage `
    -p:StoreBuild=true -p:Version=$asmVersion -p:AssemblyVersion="$pkgVersion" -p:FileVersion="$pkgVersion"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Get-ChildItem $stage -Filter *.pdb -Recurse | Remove-Item -Force

if (-not (Test-Path (Join-Path $images "StoreLogo.png"))) {
    Write-Host "==> Generating tile images" -ForegroundColor Cyan
    & powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "make-store-images.ps1")
}
Copy-Item $images (Join-Path $stage "Images") -Recurse -Force

# ----------------------------------------------------------------- manifest ----

$manifest = Get-Content (Join-Path $PSScriptRoot "AppxManifest.xml") -Raw -Encoding UTF8
$manifest = $manifest.Replace("__DISPLAY_NAME__", [System.Security.SecurityElement]::Escape($displayName)).
                      Replace("__IDENTITY_NAME__", $IdentityName).
                      Replace("__PUBLISHER__", $Publisher).
                      Replace("__PUBLISHER_DISPLAY_NAME__", [System.Security.SecurityElement]::Escape($PublisherDisplayName)).
                      Replace("__VERSION__", $pkgVersion).
                      Replace("__ARCH__", $Arch)
[System.IO.File]::WriteAllText((Join-Path $stage "AppxManifest.xml"), $manifest, (New-Object System.Text.UTF8Encoding($false)))

# -------------------------------------------------------------- sdk tooling ----

function Get-SdkTool {
    param([string]$Name)

    # 1) An installed Windows SDK, newest first.
    $kits = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (Test-Path $kits) {
        $hit = Get-ChildItem $kits -Directory -ErrorAction SilentlyContinue |
               Sort-Object Name -Descending |
               ForEach-Object { Join-Path $_.FullName "x64\$Name" } |
               Where-Object { Test-Path $_ } |
               Select-Object -First 1
        if ($hit) { return $hit }
    }

    # 2) Fall back to the SDK build tools NuGet package (no SDK install needed).
    $pkgVer  = "10.0.26100.1742"
    $pkgRoot = Join-Path $toolsDir "Microsoft.Windows.SDK.BuildTools.$pkgVer"
    if (-not (Test-Path $pkgRoot)) {
        Write-Host "==> Downloading Windows SDK build tools ($pkgVer)" -ForegroundColor Cyan
        New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
        $nupkg = Join-Path $toolsDir "sdk-buildtools.zip"
        Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/Microsoft.Windows.SDK.BuildTools/$pkgVer" `
                          -OutFile $nupkg -UseBasicParsing
        Expand-Archive -Path $nupkg -DestinationPath $pkgRoot -Force
        Remove-Item $nupkg -Force
    }

    $hit = Get-ChildItem $pkgRoot -Recurse -Filter $Name -ErrorAction SilentlyContinue |
           Where-Object { $_.FullName -match "\\x64\\" } |
           Select-Object -First 1
    if (-not $hit) { throw "Could not locate $Name" }
    return $hit.FullName
}

# --------------------------------------------------------------------- pack ----

$makeappx = Get-SdkTool -Name "makeappx.exe"
$msix = Join-Path $outDir ("{0}-{1}-{2}.msix" -f ($displayName -replace '\s+', ''), $pkgVersion, $Arch)

Write-Host "==> Packing MSIX" -ForegroundColor Cyan
& $makeappx pack /d $stage /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed" }

# --------------------------------------------------------------------- sign ----

if ($SelfSign) {
    Write-Host "==> Signing with a local test certificate" -ForegroundColor Cyan

    $cert = Get-ChildItem Cert:\CurrentUser\My |
            Where-Object { $_.Subject -eq $Publisher -and $_.NotAfter -gt (Get-Date) } |
            Select-Object -First 1
    if (-not $cert) {
        $cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher `
            -KeyUsage DigitalSignature -FriendlyName "SoundDeck MSIX test" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
    }

    $signtool = Get-SdkTool -Name "signtool.exe"
    & $signtool sign /fd SHA256 /sha1 $cert.Thumbprint /t http://timestamp.digicert.com $msix
    if ($LASTEXITCODE -ne 0) { throw "signtool sign failed" }

    $cer = Join-Path $outDir "SoundDeck-test-cert.cer"
    Export-Certificate -Cert $cert -FilePath $cer -Force | Out-Null

    Write-Host ""
    Write-Host "Signed with a TEST certificate - for local install only, never for the Store." -ForegroundColor Yellow
    Write-Host "To install it on this machine, run these two in an ADMIN PowerShell:" -ForegroundColor Yellow
    Write-Host "  Import-Certificate -FilePath `"$cer`" -CertStoreLocation Cert:\LocalMachine\TrustedPeople" -ForegroundColor DarkGray
    Write-Host "  Add-AppxPackage -Path `"$msix`"" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "==> Done: $msix" -ForegroundColor Green
if (-not $SelfSign) {
    Write-Host "    Upload this unsigned package in Partner Center - the Store signs it for you." -ForegroundColor DarkGray
}
