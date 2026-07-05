using Game.Core.Localization;
using Microsoft.JSInterop;

namespace Game.Web.Services;

/// <summary>
/// Bridges the browser's language preference and the player's explicit choice
/// into the domain <see cref="ILocalizer"/>.
///
/// Resolution order on startup: the player's persisted choice
/// (<see cref="GameState.ChosenLanguage"/>) wins; when it is null we auto-detect
/// from <c>navigator.language</c>, defaulting to English for anything that
/// isn't a recognised Chinese locale. Picking a language explicitly persists it
/// on the state so it survives reloads and stops auto-detection taking over.
/// </summary>
public sealed class LanguageService
{
    private readonly IJSRuntime _js;
    private readonly ILocalizer _localizer;
    private readonly SaveCoordinator _save;

    public LanguageService(IJSRuntime js, ILocalizer localizer, SaveCoordinator save)
    {
        _js = js;
        _localizer = localizer;
        _save = save;
    }

    /// <summary>
    /// Apply the effective language on startup — the persisted choice if there
    /// is one, otherwise the auto-detected browser language. Call after the save
    /// has loaded so <see cref="GameState.ChosenLanguage"/> is populated.
    /// </summary>
    public async Task InitAsync()
    {
        var chosen = _save.State.ChosenLanguage;
        if (chosen is { } lang)
        {
            _localizer.SetLanguage(lang);
            return;
        }

        var detected = await DetectAsync();
        _localizer.SetLanguage(detected);
    }

    /// <summary>Currently displayed language.</summary>
    public Language Current => _localizer.Current;

    /// <summary>Whether the player has explicitly chosen a language (vs. following the system).</summary>
    public bool IsFollowingSystem => _save.State.ChosenLanguage is null;

    /// <summary>
    /// Set the language from an explicit player choice: persist it on the state
    /// (so it survives reloads) and switch the display. Passing null clears the
    /// choice and reverts to following the system language immediately.
    /// </summary>
    public async Task ChooseAsync(Language? language)
    {
        _save.State.ChosenLanguage = language;
        var effective = language ?? await DetectAsync();
        _localizer.SetLanguage(effective);
        await _save.SaveNowAsync();
    }

    /// <summary>Map <c>navigator.language</c> to one of our supported languages.</summary>
    private async Task<Language> DetectAsync()
    {
        string? tag = null;
        // Named helper (see wwwroot/js/cookie-clicker.js) rather than `eval`, so
        // detection survives a strict Content-Security-Policy.
        try { tag = await _js.InvokeAsync<string?>("cookieClicker.getBrowserLanguage"); }
        catch { /* headless / no navigator — fall through to English */ }
        return LanguageDetection.FromBrowserTag(tag);
    }
}
