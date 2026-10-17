using System.Globalization;

namespace LinkTrimmer.App;

/// <summary>
/// Etykiety interfejsu zależne od języka. Użycie: <c>Loc.T("main.add")</c> / <c>Loc.T("main.confirmDelete", name)</c>.
/// Aby dodać język: dopisz kod do <see cref="Languages"/> i słownik w <see cref="Tables"/>.
/// Brak klucza w danym języku => fallback na angielski, potem sam klucz.
/// </summary>
internal static class Loc
{
    /// <summary>Dostępne języki: kod -> nazwa wyświetlana (zawsze w tym języku).</summary>
    public static readonly IReadOnlyList<(string Code, string Name)> Languages = new[]
    {
        ("pl", "Polski"),
        ("en", "English"),
        ("zh", "中文"),
    };

    public static string Current { get; private set; } = "en";

    /// <summary>Wywoływane po zmianie języka – okna odświeżają swoje teksty.</summary>
    public static event Action? Changed;

    public static void SetLanguage(string? code)
    {
        code = Normalize(code);
        if (code == Current) return;
        Current = code;
        Changed?.Invoke();
    }

    /// <summary>Ustawia język bez wywoływania zdarzenia (start aplikacji).</summary>
    public static void Init(string? savedCode) => Current = Normalize(savedCode);

    private static string Normalize(string? code)
    {
        code ??= CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        code = code.ToLowerInvariant();
        return Languages.Any(l => l.Code == code) ? code : "en";
    }

    public static string T(string key, params object[] args)
    {
        var text = Lookup(Current, key) ?? Lookup("en", key) ?? key;
        return args.Length == 0 ? text : string.Format(text, args);
    }

    private static string? Lookup(string lang, string key) =>
        Tables.TryGetValue(lang, out var t) && t.TryGetValue(key, out var v) ? v : null;

    private static readonly Dictionary<string, Dictionary<string, string>> Tables = new()
    {
        ["pl"] = new()
        {
            ["main.title"] = "LinkTrimmer – reguły",
            ["main.col.on"] = "Wł.",
            ["main.col.name"] = "Nazwa",
            ["main.col.hosts"] = "Domeny",
            ["main.col.rewrite"] = "Podmiana domeny",
            ["main.col.params"] = "Parametry",
            ["main.add"] = "Dodaj",
            ["main.edit"] = "Edytuj",
            ["main.delete"] = "Usuń",
            ["main.language"] = "Język:",
            ["main.autostart"] = "Uruchamiaj z Windowsem",
            ["main.confirmDelete"] = "Usunąć regułę „{0}”?",
            ["main.params.removeAll"] = "usuń wszystkie",
            ["main.params.keep"] = "zostaw: {0}",
            ["main.params.remove"] = "usuń: {0}",

            ["edit.titleNew"] = "Nowa reguła",
            ["edit.titleEdit"] = "Edytuj regułę",
            ["edit.name"] = "Nazwa",
            ["edit.hosts"] = "Domeny (po przecinku)",
            ["edit.hosts.hint"] = "np. x.com, twitter.com – pasują też subdomeny (www., mobile.)",
            ["edit.rewrite"] = "Podmień domenę na (opcjonalnie)",
            ["edit.rewrite.hint"] = "np. fxtwitter.com – dla lepszego embeda; puste = bez podmiany",
            ["edit.keepOnly"] = "Usuń wszystkie parametry poza wymienionymi poniżej",
            ["edit.keep"] = "Parametry do zachowania (po przecinku)",
            ["edit.keep.hint"] = "puste + zaznaczone powyżej = usuń wszystkie parametry",
            ["edit.remove"] = "Dodatkowe parametry do usunięcia (po przecinku)",
            ["edit.remove.hint"] = "gwiazdka na końcu = prefiks, np. utm_*",
            ["edit.enabled"] = "Reguła włączona",
            ["edit.save"] = "Zapisz",
            ["edit.cancel"] = "Anuluj",
            ["edit.err.hosts"] = "Podaj przynajmniej jedną domenę.",

            ["tray.open"] = "Otwórz",
            ["tray.enabled"] = "Włączone",
            ["tray.language"] = "Język",
            ["tray.autostart"] = "Uruchamiaj z Windowsem",
            ["tray.exit"] = "Wyjdź (Exit)",
            ["tray.balloon"] = "Działam w tle. Kliknij ikonę, żeby otworzyć okno.",
        },
        ["en"] = new()
        {
            ["main.title"] = "LinkTrimmer – rules",
            ["main.col.on"] = "On",
            ["main.col.name"] = "Name",
            ["main.col.hosts"] = "Domains",
            ["main.col.rewrite"] = "Domain rewrite",
            ["main.col.params"] = "Parameters",
            ["main.add"] = "Add",
            ["main.edit"] = "Edit",
            ["main.delete"] = "Delete",
            ["main.language"] = "Language:",
            ["main.autostart"] = "Start with Windows",
            ["main.confirmDelete"] = "Delete rule \"{0}\"?",
            ["main.params.removeAll"] = "remove all",
            ["main.params.keep"] = "keep: {0}",
            ["main.params.remove"] = "remove: {0}",

            ["edit.titleNew"] = "New rule",
            ["edit.titleEdit"] = "Edit rule",
            ["edit.name"] = "Name",
            ["edit.hosts"] = "Domains (comma-separated)",
            ["edit.hosts.hint"] = "e.g. x.com, twitter.com – subdomains (www., mobile.) match too",
            ["edit.rewrite"] = "Rewrite domain to (optional)",
            ["edit.rewrite.hint"] = "e.g. fxtwitter.com – for a better embed; empty = no rewrite",
            ["edit.keepOnly"] = "Remove all parameters except those listed below",
            ["edit.keep"] = "Parameters to keep (comma-separated)",
            ["edit.keep.hint"] = "empty + checked above = remove all parameters",
            ["edit.remove"] = "Extra parameters to remove (comma-separated)",
            ["edit.remove.hint"] = "trailing asterisk = prefix, e.g. utm_*",
            ["edit.enabled"] = "Rule enabled",
            ["edit.save"] = "Save",
            ["edit.cancel"] = "Cancel",
            ["edit.err.hosts"] = "Enter at least one domain.",

            ["tray.open"] = "Open",
            ["tray.enabled"] = "Enabled",
            ["tray.language"] = "Language",
            ["tray.autostart"] = "Start with Windows",
            ["tray.exit"] = "Exit",
            ["tray.balloon"] = "Running in the background. Click the icon to open the window.",
        },
        ["zh"] = new()
        {
            ["main.title"] = "LinkTrimmer – 规则",
            ["main.col.on"] = "启用",
            ["main.col.name"] = "名称",
            ["main.col.hosts"] = "域名",
            ["main.col.rewrite"] = "域名替换",
            ["main.col.params"] = "参数",
            ["main.add"] = "添加",
            ["main.edit"] = "编辑",
            ["main.delete"] = "删除",
            ["main.language"] = "语言：",
            ["main.autostart"] = "开机自动启动",
            ["main.confirmDelete"] = "要删除规则“{0}”吗？",
            ["main.params.removeAll"] = "移除全部",
            ["main.params.keep"] = "保留：{0}",
            ["main.params.remove"] = "移除：{0}",

            ["edit.titleNew"] = "新建规则",
            ["edit.titleEdit"] = "编辑规则",
            ["edit.name"] = "名称",
            ["edit.hosts"] = "域名（用逗号分隔）",
            ["edit.hosts.hint"] = "例如 x.com, twitter.com – 子域名（www.、mobile.）同样匹配",
            ["edit.rewrite"] = "将域名替换为（可选）",
            ["edit.rewrite.hint"] = "例如 fxtwitter.com – 获得更好的嵌入预览；留空 = 不替换",
            ["edit.keepOnly"] = "移除下方列出之外的所有参数",
            ["edit.keep"] = "要保留的参数（用逗号分隔）",
            ["edit.keep.hint"] = "留空且勾选上一项 = 移除所有参数",
            ["edit.remove"] = "额外要移除的参数（用逗号分隔）",
            ["edit.remove.hint"] = "末尾星号 = 前缀，例如 utm_*",
            ["edit.enabled"] = "启用此规则",
            ["edit.save"] = "保存",
            ["edit.cancel"] = "取消",
            ["edit.err.hosts"] = "请至少输入一个域名。",

            ["tray.open"] = "打开",
            ["tray.enabled"] = "已启用",
            ["tray.language"] = "语言",
            ["tray.autostart"] = "开机自动启动",
            ["tray.exit"] = "退出 (Exit)",
            ["tray.balloon"] = "正在后台运行。点击图标可打开窗口。",
        },
    };
}
