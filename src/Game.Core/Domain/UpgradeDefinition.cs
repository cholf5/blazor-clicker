namespace Game.Core.Domain;

/// <summary>
/// Kinds of effect an upgrade can apply to the game economy.
/// Kept as an enum + parameters combo (rather than a class hierarchy)
/// to keep serialization trivial.
/// </summary>
public enum UpgradeEffectKind
{
    /// <summary>Multiplies a specific building's CPS output by the given factor.</summary>
    BuildingMultiplier,

    /// <summary>Multiplies the manual click power by the given factor.</summary>
    ClickMultiplier,

    /// <summary>Multiplies global CPS from every source by the given factor.</summary>
    GlobalCpsMultiplier,

    /// <summary>Grants Cursor extra CPS per non-Cursor building owned (value = per-building bonus).</summary>
    CursorPerNonCursorBuilding,
}

/// <summary>
/// Category of upgrade — currently used to group the shop UI.
/// </summary>
public enum UpgradeCategory
{
    Building,
    Cursor,
    Clicking,
    Kitten,
}

/// <summary>
/// Immutable definition of one upgrade. The unlock predicate is evaluated
/// against a live <see cref="GameState"/>; when true the upgrade appears
/// in the shop.
/// </summary>
public sealed record UpgradeDefinition(
    string Id,
    string Name,
    string Icon,
    string Description,
    double Cost,
    UpgradeCategory Category,
    UpgradeEffectKind EffectKind,
    double EffectValue,
    BuildingId? TargetBuilding,
    Func<GameState, bool> IsUnlocked);
