using Game.Core.Domain;

namespace Game.Core.Data;

// NOTE: threshold tiers below mirror the original idle-baking genre's
// achievement pacing (a mechanic/numeric detail — see NOTICE.md). All
// achievement *names* are original wording written for this remake.

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
    // Short-scale magnitude words keyed by 1000^tier. Declared before All so
    // it is initialized before BuildAll() runs (static fields init in order).
    // Used only to derive achievement *names* from a threshold's order of
    // magnitude — these are standard English scale terms, not game text.
    private static readonly Dictionary<int, string> MagnitudeWords = new()
    {
        [2] = "million",
        [3] = "billion",
        [4] = "trillion",
        [5] = "quadrillion",
        [6] = "quintillion",
        [7] = "sextillion",
        [8] = "septillion",
        [9] = "octillion",
        [10] = "nonillion",
        [11] = "decillion",
        [12] = "undecillion",
        [13] = "duodecillion",
        [14] = "tredecillion",
        [15] = "quattuordecillion",
        [16] = "quindecillion",
    };

    public static readonly IReadOnlyList<AchievementDefinition> All = BuildAll();

    private static readonly Dictionary<string, AchievementDefinition> ById =
        All.ToDictionary(a => a.Id);

    public static AchievementDefinition Get(string id) => ById[id];

    private static IReadOnlyList<AchievementDefinition> BuildAll()
    {
        var list = new List<AchievementDefinition>();

        // ---- Bake N cookies milestones (total baked this run) ----
        // Follows the original's ×10 / ×100 alternating ladder: a few named
        // intro tiers, then a generated ramp from 1e6 up to 1e48. Names are
        // derived from the magnitude (e.g. "Trillion baker"), never copied.
        var bakeThresholds = new List<double> { 1, 1_000, 100_000 };
        for (var exp = 6; exp <= 48;)
        {
            bakeThresholds.Add(Math.Pow(10, exp));
            exp += 2;
            if (exp > 48) break;
            bakeThresholds.Add(Math.Pow(10, exp));
            exp += 1;
        }

        var bakeIntroNames = new Dictionary<double, string>
        {
            [1] = "First bite",
            [1_000] = "Kilo-baker",
            [100_000] = "Six-figure oven",
        };

        foreach (var threshold in bakeThresholds)
        {
            var t = threshold; // capture
            var name = bakeIntroNames.TryGetValue(threshold, out var intro)
                ? intro
                : BakerName(threshold);
            list.Add(new AchievementDefinition(
                Id: $"baked_{threshold:0}",
                Name: name,
                Icon: "🍪",
                Description: $"Bake {NumberFormat.Format(threshold)} cookie{(threshold == 1 ? "" : "s")} in total.",
                IsUnlocked: s => s.TotalCookiesBaked >= t));
        }

        // ---- Own N of each building ----
        // Original ownership ladder: 1, then every 50 up to 600.
        int[] ownershipMilestones =
            [1, 50, 100, 150, 200, 250, 300, 350, 400, 450, 500, 550, 600];
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
            (1,           "First tap"),
            (100,         "Warm-up"),
            (1_000,       "Finger workout"),
            (10_000,      "Persistent clicker"),
            (100_000,     "Click marathon"),
            (1_000_000,   "Million-tap fingers"),
            (10_000_000,  "Unstoppable digit"),
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

        // ---- Cookies-per-second milestones ----
        // Purely derived from CurrentCps(); no new state needed. Names built
        // from the magnitude word with an original "-per-second" theme.
        double[] cpsThresholds =
        [
            1, 10, 100, 1_000, 10_000, 100_000,
            1e6, 1e8, 1e9, 1e11, 1e12, 1e14, 1e15, 1e17, 1e18, 1e21, 1e24, 1e27, 1e30,
        ];
        foreach (var threshold in cpsThresholds)
        {
            var t = threshold;
            list.Add(new AchievementDefinition(
                Id: $"cps_{threshold:0}",
                Name: PerSecondName(threshold),
                Icon: "⏱️",
                Description: $"Reach {NumberFormat.Format(threshold)} cookie{(threshold == 1 ? "" : "s")} per second.",
                IsUnlocked: s => s.CurrentCps() >= t));
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

        // Additional golden-cookie tiers (existing two IDs above are kept as-is
        // for save compatibility; these extend the ladder further).
        (long Threshold, string Name)[] goldenMilestones =
        [
            (50,     "Gilded habit"),
            (100,    "Hundred glimmers"),
            (500,    "Golden devotee"),
            (1_000,  "Midas touch"),
            (5_000,  "Shimmer sovereign"),
        ];
        foreach (var (threshold, name) in goldenMilestones)
        {
            var t = threshold;
            list.Add(new AchievementDefinition(
                Id: $"golden_{threshold}",
                Name: name,
                Icon: "🪙",
                Description: $"Click {threshold:N0} golden cookies.",
                IsUnlocked: s => s.GoldenCookiesClicked >= t));
        }

        // ---- Upgrade ownership milestones ----
        // 83 upgrades exist in total, so the ladder tops out at "own them all".
        (int Threshold, string Name)[] upgradeMilestones =
        [
            (10,  "Well-equipped"),
            (50,  "Fully accessorised"),
            (83,  "Complete arsenal"),
        ];
        foreach (var (threshold, name) in upgradeMilestones)
        {
            var t = threshold;
            list.Add(new AchievementDefinition(
                Id: $"upgrades_{threshold}",
                Name: name,
                Icon: threshold >= 83 ? "🏆" : threshold >= 50 ? "💎" : "✨",
                Description: threshold >= 83
                    ? "Own every upgrade in the game."
                    : $"Own {threshold} upgrades.",
                IsUnlocked: s => s.PurchasedUpgrades.Count >= t));
        }

        // ---- Sugar lump achievements ----
        (long Threshold, string Name)[] lumpMilestones =
        [
            (1,    "Sweet tooth"),
            (10,   "Sugar hoard"),
            (50,   "Confectionist"),
            (100,  "Sugar baron"),
            (200,  "Saccharine tycoon"),
            (500,  "Lord of lumps"),
            (1000, "Crystalline emperor"),
        ];
        foreach (var (threshold, name) in lumpMilestones)
        {
            var t = threshold;
            list.Add(new AchievementDefinition(
                Id: $"sugar_{threshold}",
                Name: name,
                Icon: "🍬",
                Description: $"Harvest {threshold:N0} sugar lump{(threshold == 1 ? "" : "s")}.",
                IsUnlocked: s => s.SugarLumps >= t));
        }

        // ---- Ascension achievements ----
        list.Add(new AchievementDefinition(
            Id: "ascend_1",
            Name: "Reincarnate",
            Icon: "🌈",
            Description: "Ascend for the first time.",
            IsUnlocked: s => s.AscensionCount >= 1));

        // Additional ascension-count tiers (ascend_1 above kept for saves).
        (int Threshold, string Name)[] ascendMilestones =
        [
            (5,   "Serial reincarnator"),
            (10,  "Wheel of rebirth"),
            (25,  "Eternal returner"),
            (50,  "Cycle unbroken"),
        ];
        foreach (var (threshold, name) in ascendMilestones)
        {
            var t = threshold;
            list.Add(new AchievementDefinition(
                Id: $"ascend_{threshold}",
                Name: name,
                Icon: "🔄",
                Description: $"Ascend {threshold} times.",
                IsUnlocked: s => s.AscensionCount >= t));
        }

        // ---- Prestige level achievements ----
        (int Threshold, string Name)[] prestigeMilestones =
        [
            (1,      "First light"),
            (10,     "Enlightened baker"),
            (100,    "Transcendent baker"),
            (1_000,  "Celestial baker"),
            (10_000, "Astral baker"),
        ];
        foreach (var (threshold, name) in prestigeMilestones)
        {
            var t = threshold;
            list.Add(new AchievementDefinition(
                Id: $"prestige_{threshold}",
                Name: name,
                Icon: threshold >= 1_000 ? "🌌" : threshold >= 100 ? "🕊️" : "🌠",
                Description: $"Accumulate {threshold:N0} prestige level{(threshold == 1 ? "" : "s")}.",
                IsUnlocked: s => s.PrestigeLevel >= t));
        }

        return list;
    }

    /// <summary>
    /// Builds an original "… baker" name from a power-of-ten threshold, e.g.
    /// 1e12 → "Trillion baker", 1e14 → "Hundred-trillion baker".
    /// </summary>
    private static string BakerName(double threshold)
    {
        var exp = (int)Math.Round(Math.Log10(threshold));
        var tier = exp / 3;
        var remainder = exp % 3;
        var word = MagnitudeWords.TryGetValue(tier, out var w) ? w : "cosmic";
        var name = char.ToUpperInvariant(word[0]) + word[1..];
        return remainder == 2 ? $"Hundred-{word} baker" : $"{name} baker";
    }

    /// <summary>
    /// Builds an original CPS-milestone name from a threshold, e.g. 1e6 →
    /// "Million per second", 1e8 → "Hundred-million per second". Small tiers
    /// (&lt; 1e6) get simple ordinal-flavored names.
    /// </summary>
    private static string PerSecondName(double threshold)
    {
        var exp = (int)Math.Round(Math.Log10(threshold));
        if (exp < 6)
        {
            return exp switch
            {
                0 => "Steady drip",
                1 => "Gentle stream",
                2 => "Bakery current",
                3 => "Kilo-flow",
                4 => "Rising tide",
                _ => "Torrent of dough",
            };
        }

        var tier = exp / 3;
        var remainder = exp % 3;
        var word = MagnitudeWords.TryGetValue(tier, out var w) ? w : "cosmic";
        var name = char.ToUpperInvariant(word[0]) + word[1..];
        return remainder == 2 ? $"Hundred-{word} per second" : $"{name} per second";
    }
}
