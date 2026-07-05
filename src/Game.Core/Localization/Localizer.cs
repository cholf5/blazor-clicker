namespace Game.Core.Localization;

/// <summary>
/// Default <see cref="ILocalizer"/> implementation. Holds the current language
/// and resolves keys against per-language overlay dictionaries supplied by
/// <see cref="Translations"/>, falling back to English (the source language)
/// and finally to the raw key so a missing entry is visible but never fatal.
///
/// A single instance is the app-wide source of truth for the current language
/// (registered as a singleton in the web layer).
/// </summary>
public sealed class Localizer : ILocalizer
{
    private readonly IReadOnlyDictionary<string, string> _english;
    private IReadOnlyDictionary<string, string> _overlay;

    public Language Current { get; private set; }

    public event Action? OnLanguageChanged;

    public Localizer(Language initial = Language.English)
    {
        _english = Translations.For(Language.English);
        Current = initial;
        _overlay = Translations.For(initial);
    }

    public string this[string key]
    {
        get
        {
            // Overlay first (the chosen language), then English source, then the
            // raw key as a last-resort so a gap is visible rather than blank.
            if (_overlay.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)) return v;
            if (_english.TryGetValue(key, out var en) && !string.IsNullOrEmpty(en)) return en;
            return key;
        }
    }

    public string? Overlay(string key)
    {
        if (Current == Language.English) return null;
        return _overlay.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : null;
    }

    public string Format(string key, params object[] args) =>
        string.Format(this[key], args);

    public void SetLanguage(Language language)
    {
        if (language == Current) return;
        Current = language;
        _overlay = Translations.For(language);
        OnLanguageChanged?.Invoke();
    }
}
