using System.Runtime.InteropServices;
using LinkTrimmer.Core;

namespace LinkTrimmer.App;

/// <summary>
/// Nasłuchuje zmian schowka (Ctrl+C, programowe wstawienie – wszystko co zmienia schowek)
/// i przepuszcza tekst przez <see cref="LinkProcessor"/>. Obrazy i pliki są ignorowane.
/// </summary>
public sealed class ClipboardWatcher : NativeWindow, IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    [DllImport("user32.dll", SetLastError = true)] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    [DllImport("kernel32.dll")] private static extern bool SetProcessWorkingSetSize(IntPtr process, nint min, nint max);
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess(); // pseudo-uchwyt – nic nie trzeba zwalniać

    /// <summary>Link ma kilkaset znaków; dłuższy tekst (np. skopiowany dokument) pomijamy bez analizy.</summary>
    private const int MaxLinkLength = 4096;

    private readonly LinkProcessor _processor;
    private readonly System.Windows.Forms.Timer _idleTimer = new() { Interval = 3000 };
    private string? _lastWritten;
    private string? _lastOriginal;
    private long _lastOriginalAt;
    private int _repeats;

    /// <summary>Wywoływane po podmianie: (oryginał, nowy).</summary>
    public event Action<string, string>? LinkRewritten;

    public bool Enabled { get; set; } = true;

    public ClipboardWatcher(LinkProcessor processor)
    {
        _processor = processor;
        _idleTimer.Tick += (_, _) => TrimMemory();
        CreateHandle(new CreateParams { Parent = HWND_MESSAGE });
        if (!AddClipboardFormatListener(Handle))
            throw new InvalidOperationException("Nie udało się zarejestrować nasłuchu schowka.");
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLIPBOARDUPDATE) OnClipboardChanged();
        base.WndProc(ref m);
    }

    private void OnClipboardChanged()
    {
        if (!Enabled) return;

        // Plik / obraz / za długi tekst / schowek zajęty => null => nic nie robimy.
        var original = NativeClipboard.TryReadText(Handle, MaxLinkLength);
        if (original != null && original != _lastWritten) // _lastWritten: to my sami to wstawiliśmy
        {
            // Zabezpieczenie przed "przepychanką": inny program (np. menedżer schowka) może za każdym razem
            // przywracać oryginał, a my go poprawiać – bez końca. Jeśli ten sam oryginał wraca 3 razy
            // w ciągu 3 sekund, przestajemy go ruszać (zwykłe ponowne skopiowanie linku nadal działa).
            var now = Environment.TickCount64;
            if (original == _lastOriginal && now - _lastOriginalAt < 3000) _repeats++;
            else _repeats = 0;

            if (_repeats < 3)
            {
                var fixedText = _processor.Process(original);
                if (fixedText != null)
                {
                    _lastOriginal = original;
                    _lastOriginalAt = now;
                    _lastWritten = fixedText;
                    if (NativeClipboard.TryWriteText(Handle, fixedText))
                        LinkRewritten?.Invoke(original.Trim(), fixedText);
                }
            }
        }

        _idleTimer.Stop(); _idleTimer.Start(); // odliczanie do sprzątania od ostatniej zmiany
    }

    /// <summary>
    /// Po serii zmian schowka GC małej aplikacji może zwlekać ze sprzątaniem. Po chwili ciszy robimy pełne
    /// sprzątanie i oddajemy nieużywaną pamięć systemowi (w Menedżerze zadań spada do kilku MB).
    /// </summary>
    private void TrimMemory()
    {
        _idleTimer.Stop();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        SetProcessWorkingSetSize(GetCurrentProcess(), -1, -1);
    }

    public void Dispose()
    {
        _idleTimer.Dispose();
        if (Handle != IntPtr.Zero)
        {
            RemoveClipboardFormatListener(Handle);
            DestroyHandle();
        }
    }
}
