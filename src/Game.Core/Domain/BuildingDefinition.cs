namespace Game.Core.Domain;

/// <summary>
/// Immutable, data-driven definition of one building type.
/// Instances live in <see cref="Data.Buildings"/> and never change at runtime.
///
/// <see cref="Name"/> and <see cref="FlavorText"/> are the inline English
/// source strings; localized display text is resolved via the
/// <c>DisplayName</c>/<c>DisplayFlavor</c> extension methods, which look up an
/// overlay translation by id-derived key and fall back to these when absent.
/// </summary>
/// <param name="Id">Enum id used as the key in <see cref="GameState.BuildingCounts"/>.</param>
/// <param name="Name">English display name (source + fallback).</param>
/// <param name="Icon">Emoji used in the shop UI (poor man's sprite).</param>
/// <param name="BaseCost">Cost of the first one; subsequent ones grow by 1.15x per owned copy.</param>
/// <param name="BaseCps">Cookies per second one unit contributes before any multipliers.</param>
/// <param name="FlavorText">English shop-tooltip blurb (source + fallback).</param>
public sealed record BuildingDefinition(
    BuildingId Id,
    string Name,
    string Icon,
    double BaseCost,
    double BaseCps,
    string FlavorText);
