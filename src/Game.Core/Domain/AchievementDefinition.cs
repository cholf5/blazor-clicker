namespace Game.Core.Domain;

/// <summary>
/// Achievements are auto-checked every tick. Each has a predicate against
/// the game state; once satisfied the achievement stays unlocked forever.
/// </summary>
public sealed record AchievementDefinition(
    string Id,
    string Name,
    string Icon,
    string Description,
    Func<GameState, bool> IsUnlocked);
