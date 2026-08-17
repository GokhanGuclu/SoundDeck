# SoundDeck - generates the MSIX tile/logo images into packaging\Images.
# Run once (and again whenever the logo changes):
#   powershell -ExecutionPolicy Bypass -File packaging\make-store-images.ps1

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\..\assets\logo-draw.ps1"

$imagesDir = Join-Path $PSScriptRoot "Images"
New-Item -ItemType Directory -Force -Path $imagesDir | Out-Null

# name -> width,height (height omitted = square). These are the scale-100 assets;
# Windows scales them for higher DPI, and the unplated/targetsize variants are what
# the taskbar and Start search use.
$assets = @(
    @{ Name = "StoreLogo.png";                                     W = 50;  H = 50  },
    @{ Name = "Square44x44Logo.png";                               W = 44;  H = 44  },
    @{ Name = "Square44x44Logo.targetsize-24_altform-unplated.png"; W = 24; H = 24  },
    @{ Name = "Square44x44Logo.targetsize-256.png";                W = 256; H = 256 },
    @{ Name = "Square71x71Logo.png";                               W = 71;  H = 71  },
    @{ Name = "Square150x150Logo.png";                             W = 150; H = 150 },
    @{ Name = "Square310x310Logo.png";                             W = 310; H = 310 },
    @{ Name = "Wide310x150Logo.png";                               W = 310; H = 150 },
    @{ Name = "SplashScreen.png";                                  W = 620; H = 300 }
)

foreach ($a in $assets) {
    $path = Join-Path $imagesDir $a.Name
    Save-LogoPng -Path $path -W $a.W -H $a.H
    Write-Host ("  {0,-52} {1}x{2}" -f $a.Name, $a.W, $a.H)
}

Write-Host ""
Write-Host "MSIX images written to $imagesDir" -ForegroundColor Green
