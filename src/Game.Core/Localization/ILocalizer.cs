namespace Game.Core.Localization;

/// <summary>
/// Reads localized display strings by key. This is the single seam where
/// domain content and UI chrome become player-facing text — the domain layer
/// (<see cref="Domain.GameState"/>) never touches it; it keeps running on keys
/// and ids, and translation happens only when something is shown to the player.
///
/// English is the source language: every key is expected to resolve there, and
/// a key missing from a Chinese overlay transparently falls back to English
/// rather than showing a raw key or blank.
/// </summary>
public interface ILocalizer
{
    /// <summary>The language currently being displayed.</summary>
    Language Current { get; }

    /// <summary>Look up a translated string by key, falling back to English then the raw key.</summary>
    string this[string key] { get; }

    /// <summary>
    /// Return the overlay (non-English) translation for a key, or <c>null</c> when the
    /// current language is English or the key is absent from the overlay. Callers use
    /// this to fall back to an inline English source string that lives in the catalogs,
    /// keeping English as a single source of truth that can never regress.
    /// </summary>
    string? Overlay(string key);

    /// <summary>
    /// Look up a template by key and fill its <c>{0}</c>, <c>{1}</c>… placeholders.
    /// Used for programmatically-composed text such as achievement names
    /// (e.g. <c>"{0} baker"</c> filled with a magnitude word).
    /// </summary>
    string Format(string key, params object[] args);

    /// <summary>Switch the display language and notify subscribers.</summary>
    void SetLanguage(Language language);

    /// <summary>Raised after <see cref="SetLanguage"/> changes the language, so the UI can re-render.</summary>
    event Action? OnLanguageChanged;
}
