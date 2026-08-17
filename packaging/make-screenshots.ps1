# SoundPilot - pads screenshots to a Store-legal size.
#
#   powershell -ExecutionPolicy Bypass -File packaging\make-screenshots.ps1 -Source "C:\...\ekran görüntüleri"
#
# The Store requires desktop screenshots to be at least 1366x768. Captures that fall a
# few pixels short are centred on a 1920x1080 canvas filled with the colour sampled from
# the capture's own corner, so the padding blends into the shot instead of showing bars.
# Images already 1920x1080 or larger are copied through untouched.

param(
    [Parameter(Mandatory = $true)]
    [string]$Source,

    [string]$Out,
    [int]$Width = 1920,
    [int]$Height = 1080
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not $Out) { $Out = Join-Path $Source "store" }
New-Item -ItemType Directory -Force -Path $Out | Out-Null

$files = Get-ChildItem $Source -Filter *.png -File | Sort-Object Name
if (-not $files) { throw "No .png files in $Source" }

foreach ($f in $files) {
    $src = [System.Drawing.Image]::FromFile($f.FullName)
    try {
        if ($src.Width -ge $Width -and $src.Height -ge $Height) {
            Copy-Item $f.FullName (Join-Path $Out $f.Name) -Force
            Write-Host ("  {0,-20} {1}x{2}  (kopyalandi)" -f $f.Name, $src.Width, $src.Height)
            continue
        }

        # Average the four corners so the padding matches the capture's backdrop.
        $bmpSrc = New-Object System.Drawing.Bitmap($src)
        $corners = @(
            $bmpSrc.GetPixel(0, 0),
            $bmpSrc.GetPixel($bmpSrc.Width - 1, 0),
            $bmpSrc.GetPixel(0, $bmpSrc.Height - 1),
            $bmpSrc.GetPixel($bmpSrc.Width - 1, $bmpSrc.Height - 1)
        )
        $fill = [System.Drawing.Color]::FromArgb(
            [int](($corners | Measure-Object -Property R -Average).Average),
            [int](($corners | Measure-Object -Property G -Average).Average),
            [int](($corners | Measure-Object -Property B -Average).Average))

        $canvas = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($canvas)
        $g.Clear($fill)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.DrawImage($bmpSrc, [int](($Width - $bmpSrc.Width) / 2), [int](($Height - $bmpSrc.Height) / 2),
                     $bmpSrc.Width, $bmpSrc.Height)
        $g.Dispose()

        $target = Join-Path $Out $f.Name
        $canvas.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
        $canvas.Dispose()
        $bmpSrc.Dispose()

        Write-Host ("  {0,-20} {1}x{2} -> {3}x{4}" -f $f.Name, $src.Width, $src.Height, $Width, $Height)
    }
    finally {
        $src.Dispose()
    }
}

Write-Host ""
Write-Host "Store icin hazir goruntuler: $Out" -ForegroundColor Green
