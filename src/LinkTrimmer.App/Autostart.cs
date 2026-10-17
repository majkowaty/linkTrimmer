using Microsoft.Win32;

namespace LinkTrimmer.App;

/// <summary>
/// Uruchamianie z Windowsem przez standardowy wpis użytkownika HKCU\...\Run (bez uprawnień administratora).
/// Wpis uruchamia aplikację od razu schowaną w tray'u (<c>--minimized</c>). Wyłączenie usuwa wpis.
/// </summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LinkTrimmer";

    /// <summary>Polecenie, które Windows wykona przy logowaniu (ścieżka do bieżącego pliku .exe).</summary>
    private static string? Command =>
        Environment.ProcessPath is { Length: > 0 } path ? $"\"{path}\" --minimized" : null;

    /// <summary>
    /// True tylko wtedy, gdy wpis istnieje I wskazuje na ten sam plik .exe, z którego działamy
    /// (po przeniesieniu pliku stary wpis jest nieaktualny, więc pokazujemy "wyłączone").
    /// </summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) as string is { } value &&
                   string.Equals(value, Command, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <returns>True, jeśli operacja się udała.</returns>
    public static bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
            {
                if (Command is not { } cmd) return false;
                key.SetValue(ValueName, cmd, RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch { return false; } // np. rejestr zablokowany polityką firmową
    }
}
