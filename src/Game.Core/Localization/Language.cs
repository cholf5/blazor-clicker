namespace Game.Core.Localization;

/// <summary>
/// The set of languages the game can display. English is the source language
/// and the fallback for any missing translation, so it can never be "absent".
/// The two Chinese variants are authored as separate overlay dictionaries
/// (see <see cref="Translations"/>) rather than derived from one another —
/// Simplified→Traditional conversion has too many one-to-many ambiguities
/// (面→麵/面, 发→發/髮) and idiom differences to generate reliably.
/// </summary>
public enum Language
{
    English,
    SimplifiedChinese,
    TraditionalChinese,
}
