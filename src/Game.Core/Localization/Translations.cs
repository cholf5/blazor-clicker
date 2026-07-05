namespace Game.Core.Localization;

/// <summary>
/// Central access point for the per-language translation dictionaries. English
/// is the source: it defines every key the game can request (UI chrome, news
/// lines, generated-family templates), so any key missing from a Chinese
/// overlay falls back to a real English string rather than a raw key.
///
/// The two Chinese variants are authored by hand as separate overlays
/// (<see cref="TranslationsZhHans"/> / <see cref="TranslationsZhHant"/>) rather
/// than machine-converted from one another — Simplified↔Traditional has too
/// many one-to-many and idiom differences to generate reliably. They need only
/// contain keys whose value differs from English; anything absent resolves to
/// the English source automatically.
/// </summary>
public static class Translations
{
    public static IReadOnlyDictionary<string, string> For(Language language) => language switch
    {
        Language.SimplifiedChinese => TranslationsZhHans.Entries,
        Language.TraditionalChinese => TranslationsZhHant.Entries,
        _ => TranslationsEn.Entries,
    };
}
