namespace LinkTrimmer.Core;

/// <summary>Czysta logika: tekst ze schowka -> poprawiony tekst (albo null, gdy nie ma nic do zmiany).</summary>
public sealed class LinkProcessor
{
    private readonly TrimmerConfig _config;

    public LinkProcessor(TrimmerConfig? config = null) => _config = config ?? new TrimmerConfig();

    /// <returns>Nowy tekst lub <c>null</c>, jeśli wejście nie jest pojedynczym linkiem http(s) albo nic się nie zmienia.</returns>
    public string? Process(string? clipboardText)
    {
        try { return ProcessCore(clipboardText); }
        // Nietypowy/uszkodzony link albo błędna wartość w konfiguracji nie może wywrócić aplikacji –
        // w razie czego zostawiamy schowek bez zmian.
        catch (Exception e) when (e is UriFormatException or ArgumentException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private string? ProcessCore(string? clipboardText)
    {
        if (string.IsNullOrWhiteSpace(clipboardText)) return null;
        var text = clipboardText.Trim();

        // Tylko gdy cały schowek to jeden link – zwykły tekst z linkiem w środku zostaje nietknięty.
        if (text.Any(char.IsWhiteSpace)) return null;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

        var rule = _config.Rules.FirstOrDefault(r => r.Enabled && r.MatchesHost(uri.Host));

        var builder = new UriBuilder(uri);
        var pairs = SplitQuery(uri.Query);

        if (rule?.KeepParams is { } keep)
            pairs = pairs.Where(p => keep.Contains(p.Name, StringComparer.OrdinalIgnoreCase)).ToList();

        pairs = pairs.Where(p => !IsRemoved(p.Name, _config.GlobalRemoveParams) &&
                                 !(rule != null && IsRemoved(p.Name, rule.RemoveParams))).ToList();

        builder.Query = string.Join("&", pairs.Select(p => p.Raw));

        if (rule?.RewriteHost is { Length: > 0 } newHost)
        {
            // Wartość pochodzi z pliku konfiguracji (edytowalnego ręcznie) – musi być zwykłą nazwą domeny,
            // bez ścieżki, "@", portu itp. Inaczej nie ruszamy linku.
            if (Uri.CheckHostName(newHost) is not (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6))
                return null;
            builder.Host = newHost;
            builder.UserName = "";
            builder.Password = "";
            builder.Fragment = "";
        }
        if (builder.Uri.IsDefaultPort) builder.Port = -1;

        var result = builder.Uri.AbsoluteUri;
        // Jeśli różni się tylko normalizacją URI (np. dodany "/"), nie ruszamy schowka.
        if (result == uri.AbsoluteUri) return null;
        return result;
    }

    private static bool IsRemoved(string name, IEnumerable<string> patterns)
    {
        foreach (var p in patterns)
        {
            if (p.EndsWith('*'))
            {
                if (name.StartsWith(p[..^1], StringComparison.OrdinalIgnoreCase)) return true;
            }
            else if (name.Equals(p, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static List<(string Name, string Raw)> SplitQuery(string query)
    {
        var list = new List<(string, string)>();
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = part.Split('=', 2)[0];
            list.Add((Uri.UnescapeDataString(name), part));
        }
        return list;
    }
}
