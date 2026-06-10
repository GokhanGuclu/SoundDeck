Add-Type -AssemblyName System.Drawing

function Draw-Logo {
    param([System.Drawing.Graphics]$g, [int]$S)

    $scale = $S / 256.0
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    # ---- Rounded background with purple->blue gradient ----
    $radius = 56 * $scale
    $rect = New-Object System.Drawing.RectangleF(0, 0, $S, $S)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $c1 = [System.Drawing.Color]::FromArgb(139, 92, 246)
    $c2 = [System.Drawing.Color]::FromArgb(59, 130, 246)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $c1, $c2, 50.0)
    $g.FillPath($brush, $path)
    $brush.Dispose()
    $path.Dispose()

    # ---- Headphones (white) ----
    $white = [System.Drawing.Color]::White
    $pen = New-Object System.Drawing.Pen($white, (24 * $scale))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

    # headband arc (top arch)
    $bx = 64 * $scale; $by = 72 * $scale; $bw = 128 * $scale; $bh = 128 * $scale
    $g.DrawArc($pen, $bx, $by, $bw, $bh, 180, 180)
    $pen.Dispose()

    # ear cups (rounded rectangles)
    $wbrush = New-Object System.Drawing.SolidBrush($white)
    $cupW = 48 * $scale; $cupH = 78 * $scale; $cupR = 22 * $scale
    $cupTop = 132 * $scale
    $leftX  = 40 * $scale
    $rightX = ($S - 40 * $scale - $cupW)

    foreach ($cx in @($leftX, $rightX)) {
        $cp = New-Object System.Drawing.Drawing2D.GraphicsPath
        $cd = $cupR * 2
        $cp.AddArc($cx, $cupTop, $cd, $cd, 180, 90)
        $cp.AddArc($cx + $cupW - $cd, $cupTop, $cd, $cd, 270, 90)
        $cp.AddArc($cx + $cupW - $cd, $cupTop + $cupH - $cd, $cd, $cd, 0, 90)
        $cp.AddArc($cx, $cupTop + $cupH - $cd, $cd, $cd, 90, 90)
        $cp.CloseFigure()
        $g.FillPath($wbrush, $cp)
        $cp.Dispose()
    }
    $wbrush.Dispose()
}

function Get-PngBytes {
    param([int]$S)
    $bmp = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    Draw-Logo -g $g -S $S
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $bytes = $ms.ToArray()
    $ms.Dispose()
    return ,$bytes
}

# ---- 256 PNG (for README / store) ----
[System.IO.File]::WriteAllBytes("$PSScriptRoot\logo.png", (Get-PngBytes -S 256))

# ---- Multi-size .ico ----
$sizes = @(16, 32, 48, 64, 128, 256)
$images = @()
foreach ($s in $sizes) { $images += ,(Get-PngBytes -S $s) }

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
# ICONDIR
$bw.Write([UInt16]0)               # reserved
$bw.Write([UInt16]1)               # type = icon
$bw.Write([UInt16]$sizes.Count)    # count

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $len = $images[$i].Length
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim)   # width
    $bw.Write([byte]$dim)   # height
    $bw.Write([byte]0)      # color count
    $bw.Write([byte]0)      # reserved
    $bw.Write([UInt16]1)    # planes
    $bw.Write([UInt16]32)   # bit count
    $bw.Write([UInt32]$len) # bytes in resource
    $bw.Write([UInt32]$offset)
    $offset += $len
}
foreach ($img in $images) { $bw.Write($img) }
$bw.Flush()
[System.IO.File]::WriteAllBytes("$PSScriptRoot\..\AudioDeviceTrayApp\app.ico", $out.ToArray())
$bw.Dispose(); $out.Dispose()

Write-Host "assets\logo.png and AudioDeviceTrayApp\app.ico created"
