namespace Game.Core.Localization;

/// <summary>
/// Pure mapping from a browser/OS locale tag (BCP-47) to one of the game's
/// supported languages. Lives in Game.Core so it's unit-testable without the
/// web layer; the web <c>LanguageService</c> only supplies the tag from
/// <c>navigator.language</c> and delegates here.
/// </summary>
public static class LanguageDetection
{
    /// <summary>
    /// Classify a language tag. zh-CN/zh-SG/zh-Hans* (and bare zh) → Simplified;
    /// zh-TW/zh-HK/zh-MO/zh-Hant* → Traditional; everything else → English.
    /// </summary>
    public static Language FromBrowserTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return Language.English;
        var t = tag.Trim().ToLowerInvariant().Replace('_', '-');
        if (!t.StartsWith("zh")) return Language.English;

        if (t.Contains("hant") || t.Contains("-tw") || t.Contains("-hk") || t.Contains("-mo"))
            return Language.TraditionalChinese;
        // zh, zh-cn, zh-sg, zh-hans, and any other zh-* default to Simplified.
        return Language.SimplifiedChinese;
    }
}
