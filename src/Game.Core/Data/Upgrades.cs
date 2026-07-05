using Game.Core.Domain;
using Game.Core.Localization;

namespace Game.Core.Data;

/// <summary>
/// Static catalog of every upgrade. Upgrades fall into a few patterns:
///
/// * Tier upgrades — one per building, one per tier. Each doubles that
///   building's CPS and is unlocked when you own N of that building.
/// * Clicking upgrades — flat multipliers on manual click power.
/// * Cursor synergies — Cursor gains flat CPS per non-Cursor building owned.
/// * Kitten / global — multiplicative bonuses on all CPS.
/// </summary>
public static class Upgrades
{
    // Standard tier costs (multiples of the building's base cost) and
    // unlock thresholds (buildings owned). Prefix names are original. TierKey
    // keys the localized prefix word in the translation dictionaries.
    private static readonly (int Ownership, int CostMultiplier, string TierPrefix, string TierKey)[] Tiers =
    [
        (1,   100,        "Improved",  "improved"),
        (5,   500,        "Refined",   "refined"),
        (25,  50_000,     "Advanced",  "advanced"),
        (50,  5_000_000,  "Legendary", "legendary"),
    ];

    public static readonly IReadOnlyList<UpgradeDefinition> All = BuildAll();

    private static readonly Dictionary<string, UpgradeDefinition> ById =
        All.ToDictionary(u => u.Id);

    public static UpgradeDefinition Get(string id) => ById[id];

    public static bool Exists(string id) => ById.ContainsKey(id);

    private static IReadOnlyList<UpgradeDefinition> BuildAll()
    {
        var list = new List<UpgradeDefinition>();

        // ---- Building tier upgrades: each doubles that building's CPS ----
        foreach (var building in Buildings.All)
        {
            foreach (var (threshold, costMult, tierPrefix, tierKey) in Tiers)
            {
                var id = $"tier_{building.Id}_{threshold}";
                var name = $"{tierPrefix} {building.Name.ToLowerInvariant()}s";
                var cost = building.BaseCost * costMult;
                var thresholdCaptured = threshold;
                var buildingIdCaptured = building.Id;
                var tierKeyCaptured = tierKey;

                list.Add(new UpgradeDefinition(
                    Id: id,
                    Name: name,
                    Icon: building.Icon,
                    Description: $"{building.Name}s are twice as efficient. (Requires {threshold} owned.)",
                    Cost: cost,
                    Category: UpgradeCategory.Building,
                    EffectKind: UpgradeEffectKind.BuildingMultiplier,
                    EffectValue: 2.0,
                    TargetBuilding: building.Id,
                    IsUnlocked: state => state.BuildingCounts.TryGetValue(buildingIdCaptured, out var c) && c >= thresholdCaptured)
                {
                    // Compose "<tier prefix> <building>" and the effect blurb from
                    // localized sub-pieces: the tier prefix word and the (already
                    // localized) building name.
                    NameOverlay = loc => loc.OverlayFormat(
                        $"upgrade.tier.{tierKeyCaptured}.name", Buildings.Get(buildingIdCaptured).DisplayName(loc)),
                    DescOverlay = loc => loc.OverlayFormat(
                        "upgrade.tier.desc", Buildings.Get(buildingIdCaptured).DisplayName(loc), thresholdCaptured),
                });
            }
        }

        // ---- Clicking upgrades ----
        list.Add(new UpgradeDefinition(
            Id: "click_reinforced_finger",
            Name: "Ergonomic mouse",
            Icon: "👆",
            Description: "Manual clicks and cursor buildings are twice as productive.",
            Cost: 100,
            Category: UpgradeCategory.Clicking,
            EffectKind: UpgradeEffectKind.ClickMultiplier,
            EffectValue: 2.0,
            TargetBuilding: null,
            IsUnlocked: state => state.TotalCookiesBaked >= 50));

        list.Add(new UpgradeDefinition(
            Id: "click_carpal_tunnel",
            Name: "Wrist trainer",
            Icon: "🧴",
            Description: "Manual clicks and cursor buildings are twice as productive.",
            Cost: 500,
            Category: UpgradeCategory.Clicking,
            EffectKind: UpgradeEffectKind.ClickMultiplier,
            EffectValue: 2.0,
            TargetBuilding: null,
            IsUnlocked: state => state.PurchasedUpgrades.Contains("click_reinforced_finger") && state.TotalCookiesBaked >= 200));

        list.Add(new UpgradeDefinition(
            Id: "click_ambidextrous",
            Name: "Two-handed clicker",
            Icon: "🖐️",
            Description: "Manual clicks and cursor buildings are twice as productive.",
            Cost: 10_000,
            Category: UpgradeCategory.Clicking,
            EffectKind: UpgradeEffectKind.ClickMultiplier,
            EffectValue: 2.0,
            TargetBuilding: null,
            IsUnlocked: state => state.PurchasedUpgrades.Contains("click_carpal_tunnel") && state.TotalCookiesBaked >= 5_000));

        list.Add(new UpgradeDefinition(
            Id: "click_gilded_stylus",
            Name: "Gilded stylus",
            Icon: "🖋️",
            Description: "Every manual click hits three times harder.",
            Cost: 500_000_000,
            Category: UpgradeCategory.Clicking,
            EffectKind: UpgradeEffectKind.ClickMultiplier,
            EffectValue: 3.0,
            TargetBuilding: null,
            IsUnlocked: state => state.PurchasedUpgrades.Contains("click_ambidextrous") && state.TotalCookiesBaked >= 1_000_000));

        // ---- Cursor synergies ----
        list.Add(new UpgradeDefinition(
            Id: "cursor_thousand_fingers",
            Name: "Helper hands",
            Icon: "✋",
            Description: "Cursors gain +0.1 CPS for each non-Cursor building owned.",
            Cost: 100_000,
            Category: UpgradeCategory.Cursor,
            EffectKind: UpgradeEffectKind.CursorPerNonCursorBuilding,
            EffectValue: 0.1,
            TargetBuilding: null,
            IsUnlocked: state => state.BuildingCounts.TryGetValue(BuildingId.Cursor, out var c) && c >= 25));

        list.Add(new UpgradeDefinition(
            Id: "cursor_million_fingers",
            Name: "Helper hands II",
            Icon: "🖐️",
            Description: "Multiplies the Helper hands bonus by 5.",
            Cost: 10_000_000,
            Category: UpgradeCategory.Cursor,
            EffectKind: UpgradeEffectKind.CursorPerNonCursorBuilding,
            EffectValue: 0.5, // 5x the 0.1 base = 0.5 total; effect is additive on top
            TargetBuilding: null,
            IsUnlocked: state => state.PurchasedUpgrades.Contains("cursor_thousand_fingers")
                                 && state.BuildingCounts.TryGetValue(BuildingId.Cursor, out var c) && c >= 50));

        list.Add(new UpgradeDefinition(
            Id: "cursor_billion_fingers",
            Name: "Helper hands III",
            Icon: "🙌",
            Description: "Multiplies the Helper hands bonus by another 10.",
            Cost: 100_000_000,
            Category: UpgradeCategory.Cursor,
            EffectKind: UpgradeEffectKind.CursorPerNonCursorBuilding,
            EffectValue: 5.0,
            TargetBuilding: null,
            IsUnlocked: state => state.PurchasedUpgrades.Contains("cursor_million_fingers")
                                 && state.BuildingCounts.TryGetValue(BuildingId.Cursor, out var c) && c >= 100));

        // ---- Global CPS boosts ----
        list.Add(new UpgradeDefinition(
            Id: "global_kitten_helpers",
            Name: "Feline apprentices",
            Icon: "🐱",
            Description: "Global CPS is multiplied by 1.10.",
            Cost: 9_000_000,
            Category: UpgradeCategory.Kitten,
            EffectKind: UpgradeEffectKind.GlobalCpsMultiplier,
            EffectValue: 1.1,
            TargetBuilding: null,
            IsUnlocked: state => state.TotalCookiesBaked >= 1_000_000));

        list.Add(new UpgradeDefinition(
            Id: "global_kitten_workers",
            Name: "Feline workforce",
            Icon: "🐈",
            Description: "Global CPS is multiplied by 1.25.",
            Cost: 9_000_000_000,
            Category: UpgradeCategory.Kitten,
            EffectKind: UpgradeEffectKind.GlobalCpsMultiplier,
            EffectValue: 1.25,
            TargetBuilding: null,
            IsUnlocked: state => state.PurchasedUpgrades.Contains("global_kitten_helpers")
                                 && state.TotalCookiesBaked >= 100_000_000));

        list.Add(new UpgradeDefinition(
            Id: "global_kitten_engineers",
            Name: "Feline engineers",
            Icon: "🐈‍⬛",
            Description: "Global CPS is multiplied by 1.5.",
            Cost: 90_000_000_000_000d,
            Category: UpgradeCategory.Kitten,
            EffectKind: UpgradeEffectKind.GlobalCpsMultiplier,
            EffectValue: 1.5,
            TargetBuilding: null,
            IsUnlocked: state => state.PurchasedUpgrades.Contains("global_kitten_workers")
                                 && state.TotalCookiesBaked >= 10_000_000_000));

        list.Add(new UpgradeDefinition(
            Id: "global_kitten_professors",
            Name: "Feline professors",
            Icon: "🎓",
            Description: "Global CPS is multiplied by 1.75.",
            Cost: 900_000_000_000_000_000d,
            Category: UpgradeCategory.Kitten,
            EffectKind: UpgradeEffectKind.GlobalCpsMultiplier,
            EffectValue: 1.75,
            TargetBuilding: null,
            IsUnlocked: state => state.PurchasedUpgrades.Contains("global_kitten_engineers")
                                 && state.TotalCookiesBaked >= 1_000_000_000_000d));

        return list;
    }
}
