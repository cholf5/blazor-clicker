using Game.Core.Domain;

namespace Game.Core.Data;

/// <summary>
/// Static catalog of achievements. Families:
///
/// * "Bake N cookies" milestones (across a run)
/// * "Own N of a building" milestones
/// * Miscellaneous — click counts, upgrades purchased, golden cookies
/// * Meta — sugar lumps harvested, ascensions performed
/// </summary>
public static class Achievements
{
    public static readonly IReadOnlyList<AchievementDefinition> All = BuildAll();

    private static readonly Dictionary<string, AchievementDefinition> ById =
        All.ToDictionary(a => a.Id);

    public static AchievementDefinition Get(string id) => ById[id];

    private static IReadOnlyList<AchievementDefinition> BuildAll()
    {
        var list = new List<AchievementDefinition>();

        // ---- Bake N cookies milestones (total baked this run) ----
        (double Threshold, string Name)[] bakeMilestones =
        [
            (1,                    "First bite"),
            (1_000,                "Kilo-baker"),
            (100_000,              "Six-figure oven"),
            (1_000_000,            "Millionaire baker"),
            (100_000_000,          "Industrial scale"),
            (1_000_000_000,        "Billion-cookie club"),
            (100_000_000_000,      "Hundred-billion baker"),
            (1_000_000_000_000,    "Trillion baker"),
            (1e15,                 "Quadrillion baker"),
            (1e18,                 "Cosmic baker"),
            (1e21,                 "Sextillion baker"),
            (1e24,                 "Septillion baker"),
        ];
        foreach (var (threshold, name) in bakeMilestones)
        {
            var t = threshold; // capture
            list.Add(new AchievementDefinition(
                Id: $"baked_{threshold:0}",
                Name: name,
                Icon: "🍪",
                Description: $"Bake {threshold:N0} cookies in total.",
                IsUnlocked: s => s.TotalCookiesBaked >= t));
        }

        // ---- Own N of each building ----
        int[] ownershipMilestones = [1, 50, 100, 150];
        foreach (var building in Buildings.All)
        {
            foreach (var count in ownershipMilestones)
            {
                var bid = building.Id;
                var c = count;
                list.Add(new AchievementDefinition(
                    Id: $"own_{building.Id}_{count}",
                    Name: $"{count}× {building.Name.ToLowerInvariant()}",
                    Icon: building.Icon,
                    Description: $"Own {count} {building.Name.ToLowerInvariant()}{(count == 1 ? "" : "s")}.",
                    IsUnlocked: s => s.BuildingCounts.TryGetValue(bid, out var owned) && owned >= c));
            }
        }

        // ---- Clicking milestones ----
        (int Threshold, string Name)[] clickMilestones =
        [
            (100,      "Warm-up"),
            (1_000,    "Finger workout"),
            (10_000,   "Persistent clicker"),
            (100_000,  "Click marathon"),
        ];
        foreach (var (threshold, name) in clickMilestones)
        {
            var t = threshold;
            list.Add(new AchievementDefinition(
                Id: $"clicks_{threshold}",
                Name: name,
                Icon: "👆",
                Description: $"Manually click the big cookie {threshold:N0} times.",
                IsUnlocked: s => s.HandmadeClicks >= t));
        }

        // ---- Miscellaneous ----
        list.Add(new AchievementDefinition(
            Id: "golden_first",
            Name: "First gleam",
            Icon: "🌟",
            Description: "Click a golden cookie.",
            IsUnlocked: s => s.GoldenCookiesClicked >= 1));

        list.Add(new AchievementDefinition(
            Id: "golden_seven",
            Name: "Sevenfold luck",
            Icon: "🍀",
            Description: "Click 7 golden cookies.",
            IsUnlocked: s => s.GoldenCookiesClicked >= 7));

        list.Add(new AchievementDefinition(
            Id: "upgrades_10",
            Name: "Well-equipped",
            Icon: "✨",
            Description: "Own 10 upgrades.",
            IsUnlocked: s => s.PurchasedUpgrades.Count >= 10));

        list.Add(new AchievementDefinition(
            Id: "upgrades_50",
            Name: "Fully accessorised",
            Icon: "💎",
            Description: "Own 50 upgrades.",
            IsUnlocked: s => s.PurchasedUpgrades.Count >= 50));

        // ---- Sugar lump achievements ----
        (long Threshold, string Name)[] lumpMilestones =
        [
            (1,   "Sweet tooth"),
            (10,  "Sugar hoard"),
            (50,  "Confectionist"),
        ];
        foreach (var (threshold, name) in lumpMilestones)
        {
            var t = threshold;
            list.Add(new AchievementDefinition(
                Id: $"sugar_{threshold}",
                Name: name,
                Icon: "🍬",
                Description: $"Harvest {threshold} sugar lump{(threshold == 1 ? "" : "s")}.",
                IsUnlocked: s => s.SugarLumps >= t));
        }

        // ---- Ascension achievements ----
        list.Add(new AchievementDefinition(
            Id: "ascend_1",
            Name: "Reincarnate",
            Icon: "🌈",
            Description: "Ascend for the first time.",
            IsUnlocked: s => s.AscensionCount >= 1));

        list.Add(new AchievementDefinition(
            Id: "prestige_10",
            Name: "Enlightened baker",
            Icon: "🌠",
            Description: "Accumulate 10 prestige levels.",
            IsUnlocked: s => s.PrestigeLevel >= 10));

        list.Add(new AchievementDefinition(
            Id: "prestige_100",
            Name: "Transcendent baker",
            Icon: "🕊️",
            Description: "Accumulate 100 prestige levels.",
            IsUnlocked: s => s.PrestigeLevel >= 100));

        return list;
    }
}
