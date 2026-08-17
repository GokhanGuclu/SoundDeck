# SoundDeck - generates assets\logo.png (README / Store) and AudioDeviceTrayApp\app.ico
# The drawing itself lives in logo-draw.ps1 so the MSIX tile images can reuse it.

. "$PSScriptRoot\logo-draw.ps1"

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
