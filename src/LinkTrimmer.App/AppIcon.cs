using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace LinkTrimmer.App;

/// <summary>Ikona "LT" na przezroczystym tle, rysowana w kodzie (ostra w każdym rozmiarze/DPI).</summary>
internal static class AppIcon
{
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>Ikona w rozmiarze ikon tray'a bieżącego DPI.</summary>
    public static Icon CreateTray() => Create(Math.Max(SystemInformation.SmallIconSize.Width, 16));

    public static Icon Create(int size)
    {
        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb); // tło domyślnie przezroczyste
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using var path = new GraphicsPath();
            path.AddString("LT", new FontFamily("Segoe UI"), (int)FontStyle.Bold, 100f, PointF.Empty, StringFormat.GenericTypographic);

            // Dopasuj napis do całej ikony (z małym marginesem na obrys).
            var b = path.GetBounds();
            var outline = Math.Max(1f, size / 14f);
            var margin = outline / 2f + 0.5f;
            var scale = Math.Min((size - 2 * margin) / b.Width, (size - 2 * margin) / b.Height);
            using var m = new Matrix();
            m.Translate(-b.X, -b.Y);
            m.Scale(scale, scale, MatrixOrder.Append);
            m.Translate((size - b.Width * scale) / 2f, (size - b.Height * scale) / 2f, MatrixOrder.Append);
            path.Transform(m);

            // Ciemny obrys + jasna wypełniona litera – czytelne i na jasnym, i na ciemnym pasku zadań.
            using var pen = new Pen(Color.FromArgb(230, 20, 24, 36), outline) { LineJoin = LineJoin.Round };
            using var fill = new SolidBrush(Color.FromArgb(255, 79, 195, 247));
            g.DrawPath(pen, path);
            g.FillPath(fill, path);
        }

        var hIcon = bmp.GetHicon();
        try { return (Icon)Icon.FromHandle(hIcon).Clone(); }
        finally { DestroyIcon(hIcon); }
    }
}
