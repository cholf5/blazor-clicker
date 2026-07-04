using Game.Core.Domain;

namespace Game.Core.Data;

/// <summary>
/// Display grouping for an achievement. Purely a UI concern, but kept in
/// Game.Core so the mapping is data-driven and unit-testable — the web layer
/// just iterates <see cref="AchievementCategories.All"/> and filters.
/// </summary>
/// <param name="Key">Stable identifier used as the tab key.</param>
/// <param name="Label">Human-readable tab label.</param>
/// <param name="Icon">Emoji shown on the tab.</param>
/// <param name="Prefixes">Achievement-id prefixes that belong to this category.</param>
public sealed record AchievementCategory(
    string Key,
    string Label,
    string Icon,
    IReadOnlyList<string> Prefixes)
{
    public bool Matches(string achievementId) =>
        Prefixes.Any(achievementId.StartsWith);
}

/// <summary>
/// Ordered catalog of achievement categories. Every achievement id must match
/// exactly one category here — <see cref="AchievementCategoriesTests"/> guards
/// that invariant so a newly-added family can't silently fall through.
/// </summary>
public static class AchievementCategories
{
    public static readonly IReadOnlyList<AchievementCategory> All =
    [
        new("baking",     "Baking",         "🍪", ["baked_"]),
        new("buildings",  "Buildings",      "🏠", ["own_"]),
        new("production", "Production",     "⏱️", ["cps_"]),
        new("clicking",   "Clicking",       "👆", ["clicks_", "handmade_"]),
        new("golden",     "Golden cookies", "🌟", ["golden_", "combo_"]),
        new("upgrades",   "Upgrades",       "✨", ["upgrades_"]),
        new("sugar",      "Sugar lumps",    "🍬", ["sugar_"]),
        new("prestige",   "Prestige",       "🌈", ["ascend_", "prestige_"]),
        new("dedication", "Dedication",     "🕰️", ["playtime_"]),
    ];

    /// <summary>Returns the category a given achievement belongs to, or null if none match.</summary>
    public static AchievementCategory? CategoryOf(AchievementDefinition achievement) =>
        All.FirstOrDefault(c => c.Matches(achievement.Id));
}
