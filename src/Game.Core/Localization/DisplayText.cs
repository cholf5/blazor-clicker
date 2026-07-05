using Game.Core.Data;
using Game.Core.Domain;

namespace Game.Core.Localization;

/// <summary>
/// The display seam: turns data-driven definitions into localized, player-facing
/// text. Every method resolves in the same order — an explicit overlay closure
/// on the definition (used by generated families), then an id-derived overlay
/// key, then the inline English source on the definition. English therefore can
/// never regress: a missing Chinese entry transparently shows the source string.
///
/// Key scheme (see docs/plans/2026-07-05-i18n-design.md):
///   building.&lt;id&gt;.name / .desc
///   upgrade.&lt;id&gt;.name / .desc
///   achievement.&lt;id&gt;.name / .desc
/// ids are lower-cased so BuildingId.Cursor → "building.cursor.*".
/// </summary>
public static class DisplayText
{
    // ---- Buildings ----------------------------------------------------------

    public static string DisplayName(this BuildingDefinition def, ILocalizer loc) =>
        loc.Overlay($"building.{Key(def.Id)}.name") ?? def.Name;

    public static string DisplayFlavor(this BuildingDefinition def, ILocalizer loc) =>
        loc.Overlay($"building.{Key(def.Id)}.desc") ?? def.FlavorText;

    // ---- Upgrades -----------------------------------------------------------

    public static string DisplayName(this UpgradeDefinition def, ILocalizer loc) =>
        def.NameOverlay?.Invoke(loc) ?? loc.Overlay($"upgrade.{def.Id}.name") ?? def.Name;

    public static string DisplayDescription(this UpgradeDefinition def, ILocalizer loc) =>
        def.DescOverlay?.Invoke(loc) ?? loc.Overlay($"upgrade.{def.Id}.desc") ?? def.Description;

    // ---- Achievements -------------------------------------------------------

    public static string DisplayName(this AchievementDefinition def, ILocalizer loc) =>
        def.NameOverlay?.Invoke(loc) ?? loc.Overlay($"achievement.{def.Id}.name") ?? def.Name;

    public static string DisplayDescription(this AchievementDefinition def, ILocalizer loc) =>
        def.DescOverlay?.Invoke(loc) ?? loc.Overlay($"achievement.{def.Id}.desc") ?? def.Description;

    // ---- Achievement categories --------------------------------------------

    public static string DisplayLabel(this AchievementCategory cat, ILocalizer loc) =>
        loc.Overlay($"category.{cat.Key}.label") ?? cat.Label;

    private static string Key(BuildingId id) => id.ToString().ToLowerInvariant();

    /// <summary>
    /// Look up an overlay template and fill its placeholders, returning
    /// <c>null</c> when the overlay is absent (English, or untranslated) so the
    /// caller falls back to its inline English source. Generated families
    /// (tier upgrades, magnitude baker names) use this to compose localized
    /// text from sub-pieces without ever hard-coding a language in the catalog.
    /// </summary>
    public static string? OverlayFormat(this ILocalizer loc, string key, params object[] args)
    {
        var template = loc.Overlay(key);
        return template is null ? null : string.Format(template, args);
    }
}
