namespace Game.Core.Domain;

using Game.Core.Localization;

/// <summary>
/// Achievements are auto-checked every tick. Each has a predicate against
/// the game state; once satisfied the achievement stays unlocked forever.
///
/// <see cref="Name"/> and <see cref="Description"/> are the inline English
/// source strings. <see cref="NameOverlay"/> / <see cref="DescOverlay"/> are
/// optional closures that produce the localized string for the current
/// language (used by generated families such as magnitude-based baker names,
/// which compose their text from sub-keys); when null, display falls back to
/// an id-derived overlay key and finally to the English source.
/// </summary>
public sealed record AchievementDefinition(
    string Id,
    string Name,
    string Icon,
    string Description,
    Func<GameState, bool> IsUnlocked)
{
    /// <summary>Optional localized-name factory; overrides the id-derived overlay lookup.</summary>
    public Func<ILocalizer, string?>? NameOverlay { get; init; }

    /// <summary>Optional localized-description factory; overrides the id-derived overlay lookup.</summary>
    public Func<ILocalizer, string?>? DescOverlay { get; init; }
}
