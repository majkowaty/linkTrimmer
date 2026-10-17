# Generuje src/LinkTrimmer.App/app.ico (napis "LT" na przezroczystym tle) – ten sam wygląd co AppIcon.cs.
# Uruchom: powershell -File tools/make-icon.ps1
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot '..\src\LinkTrimmer.App\app.ico'
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256

function New-Png([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddString('LT', (New-Object System.Drawing.FontFamily 'Segoe UI'), [int][System.Drawing.FontStyle]::Bold, 100.0,
        (New-Object System.Drawing.PointF 0, 0), [System.Drawing.StringFormat]::GenericTypographic)
    $b = $path.GetBounds()
    $outline = [Math]::Max(1.0, $size / 14.0)
    $margin = $outline / 2 + 0.5
    $scale = [Math]::Min(($size - 2 * $margin) / $b.Width, ($size - 2 * $margin) / $b.Height)
    $m = New-Object System.Drawing.Drawing2D.Matrix
    $m.Translate(-$b.X, -$b.Y)
    $m.Scale($scale, $scale, 'Append')
    $m.Translate(($size - $b.Width * $scale) / 2, ($size - $b.Height * $scale) / 2, 'Append')
    $path.Transform($m)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(230, 20, 24, 36)), $outline
    $pen.LineJoin = 'Round'
    $fill = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 79, 195, 247))
    $g.DrawPath($pen, $path)
    $g.FillPath($fill, $path)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

$pngs = $sizes | ForEach-Object { , (New-Png $_) }
$fs = [System.IO.File]::Create($out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $pngs[$i].Length
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$len); $w.Write([uint32]$offset)
    $offset += $len
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close()
Write-Host "Zapisano $out"
