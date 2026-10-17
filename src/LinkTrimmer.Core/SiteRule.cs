namespace LinkTrimmer.Core;

/// <summary>Reguła dla konkretnego serwisu (dopasowanie po hoście).</summary>
public sealed class SiteRule
{
    public string Name { get; set; } = "";

    /// <summary>Hosty, których dotyczy reguła (pasuje też każda subdomena, np. www., mobile.).</summary>
    public List<string> Hosts { get; set; } = new();

    /// <summary>Jeśli ustawione – host zostanie podmieniony (np. x.com -> fxtwitter.com dla lepszego embeda).</summary>
    public string? RewriteHost { get; set; }

    /// <summary>Jeśli ustawione – zostają TYLKO te parametry query (pusta lista = usuń wszystkie).</summary>
    public List<string>? KeepParams { get; set; }

    /// <summary>Dodatkowe parametry do usunięcia (obsługuje wildcard na końcu, np. "utm_*").</summary>
    public List<string> RemoveParams { get; set; } = new();

    public bool Enabled { get; set; } = true;

    internal bool MatchesHost(string host)
    {
        foreach (var h in Hosts)
            if (host.Equals(h, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
