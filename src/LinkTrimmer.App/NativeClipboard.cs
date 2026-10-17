using System.Runtime.InteropServices;

namespace LinkTrimmer.App;

/// <summary>
/// Minimalny odczyt/zapis tekstu w schowku bezpośrednio przez WinAPI (bez warstwy OLE z System.Windows.Forms.Clipboard).
/// Dzięki temu nie powstają obiekty COM przy każdej zmianie schowka, a odczyt jest tani.
/// </summary>
internal static class NativeClipboard
{
    private const uint CF_BITMAP = 2, CF_DIB = 8, CF_UNICODETEXT = 13, CF_HDROP = 15;
    private const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetClipboardData(uint format, IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, nuint bytes);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern nuint GlobalSize(IntPtr hMem);

    /// <summary>Otwarcie schowka bywa chwilowo zajęte przez inny program – próbujemy kilka razy.</summary>
    private static bool Open(IntPtr owner)
    {
        for (var i = 0; i < 10; i++)
        {
            if (OpenClipboard(owner)) return true;
            Thread.Sleep(10);
        }
        return false;
    }

    /// <summary>
    /// Zwraca tekst ze schowka albo null, gdy: schowek zajęty, brak tekstu, jest plik/obraz
    /// albo tekst dłuższy niż <paramref name="maxChars"/> (wtedy nie jest nawet kopiowany do pamięci).
    /// </summary>
    public static string? TryReadText(IntPtr owner, int maxChars)
    {
        // Te sprawdzenia nie wymagają otwierania schowka.
        if (IsClipboardFormatAvailable(CF_HDROP) || IsClipboardFormatAvailable(CF_BITMAP) ||
            IsClipboardFormatAvailable(CF_DIB) || !IsClipboardFormatAvailable(CF_UNICODETEXT))
            return null;

        if (!Open(owner)) return null;
        try
        {
            var h = GetClipboardData(CF_UNICODETEXT);
            if (h == IntPtr.Zero) return null;
            var size = GlobalSize(h);
            if (size > (nuint)((maxChars + 1) * 2)) return null; // za duże jak na link
            var chars = (int)(size / 2);
            if (chars == 0) return null;

            var p = GlobalLock(h);
            if (p == IntPtr.Zero) return null;
            try
            {
                // Czytamy tylko tyle znaków, ile faktycznie ma blok pamięci (obcy program mógł nie dodać
                // końcowego zera – PtrToStringUni bez długości czytałoby wtedy poza blok).
                var s = Marshal.PtrToStringUni(p, chars);
                var nul = s.IndexOf('\0');
                return nul >= 0 ? s[..nul] : s;
            }
            finally { GlobalUnlock(h); }
        }
        finally { CloseClipboard(); }
    }

    /// <summary>Zastępuje zawartość schowka podanym tekstem (Unicode).</summary>
    public static bool TryWriteText(IntPtr owner, string text)
    {
        var bytes = (text.Length + 1) * 2;
        var hMem = GlobalAlloc(GMEM_MOVEABLE, (nuint)bytes);
        if (hMem == IntPtr.Zero) return false;

        var p = GlobalLock(hMem);
        if (p == IntPtr.Zero) { GlobalFree(hMem); return false; }
        Marshal.Copy((text + "\0").ToCharArray(), 0, p, text.Length + 1);
        GlobalUnlock(hMem);

        if (!Open(owner)) { GlobalFree(hMem); return false; }
        try
        {
            if (!EmptyClipboard()) { GlobalFree(hMem); return false; }
            if (SetClipboardData(CF_UNICODETEXT, hMem) == IntPtr.Zero) { GlobalFree(hMem); return false; }
            return true; // od teraz pamięcią zarządza system – nie wolno jej zwalniać
        }
        finally { CloseClipboard(); }
    }
}
