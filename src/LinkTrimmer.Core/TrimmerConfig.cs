using System.Text.Json;

namespace LinkTrimmer.Core;

public sealed class TrimmerConfig
{
    /// <summary>Parametry śledzące usuwane z każdego linku (wildcard '*' na końcu).</summary>
    public List<string> GlobalRemoveParams { get; set; } = new()
    {
        "utm_*", "fbclid", "gclid", "dclid", "gbraid", "wbraid", "msclkid", "yclid",
        "igshid", "igsh", "mc_eid", "mc_cid", "_ga", "_gl", "ref_src", "ref_url",
        "twclid", "ttclid", "li_fat_id", "mkt_tok", "vero_id", "spm", "scid"
    };

    /// <summary>Kod języka interfejsu ("pl", "en"); null = wykryj z systemu.</summary>
    public string? Language { get; set; }

    public List<SiteRule> Rules { get; set; } = DefaultRules();

    public static List<SiteRule> DefaultRules() => new()
    {
        new SiteRule { Name = "Twitter/X", Hosts = { "twitter.com", "x.com" }, RewriteHost = "fxtwitter.com", KeepParams = new() },
        new SiteRule { Name = "Instagram", Hosts = { "instagram.com" }, RewriteHost = "kkinstagram.com", KeepParams = new() },
        new SiteRule { Name = "TikTok", Hosts = { "tiktok.com" }, RewriteHost = "vxtiktok.com", KeepParams = new() },
        new SiteRule { Name = "Reddit", Hosts = { "reddit.com" }, KeepParams = new() },
        new SiteRule { Name = "YouTube", Hosts = { "youtube.com" }, KeepParams = new() { "v", "t", "list", "index" } },
        new SiteRule { Name = "YouTube (short)", Hosts = { "youtu.be" }, KeepParams = new() { "t" } },
        new SiteRule { Name = "Facebook", Hosts = { "facebook.com" }, RemoveParams = { "mibextid", "__cft__*", "__tn__", "ref", "refsrc" } },
    };

    public static TrimmerConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            var def = new TrimmerConfig();
            def.Save(path);
            return def;
        }
        return JsonSerializer.Deserialize<TrimmerConfig>(File.ReadAllText(path), JsonOpts) ?? new TrimmerConfig();
    }

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpts));

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
}
