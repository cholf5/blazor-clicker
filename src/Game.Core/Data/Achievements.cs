using Game.Core.Domain;
using Game.Core.Localization;

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
///
/// Localization: <see cref="AchievementDefinition.Name"/>/<c>Description</c>
/// hold the English source. Fixed-name families rely on id-derived overlay keys
/// (<c>achievement.&lt;id&gt;.name</c>); the two magnitude-generated families
/// (baked / cps ramps) attach <c>NameOverlay</c> closures that compose the
/// Chinese from a shared template plus an English magnitude phrase (magnitude
/// words stay English by design — see the i18n design doc). Descriptions carry
/// numbers, so every family attaches a <c>DescOverlay</c> that fills a Chinese
/// template with a <see cref="NumberFormat"/>-formatted value (English units).
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

    /// <summary>Look up an achievement by id without throwing on an unknown id.</summary>
    public static bool TryGet(string id, out AchievementDefinition? def) => ById.TryGetValue(id, out def);

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
            var isIntro = bakeIntroNames.TryGetValue(threshold, out var intro);
            var name = isIntro ? intro! : BakerName(threshold);
            var phrase = MagnitudePhrase(threshold);
            var formatted = NumberFormat.Format(threshold);
            list.Add(new AchievementDefinition(
                Id: $"baked_{threshold:0}",
                Name: name,
                Icon: "🍪",
                Description: $"Bake {formatted} cookie{(threshold == 1 ? "" : "s")} in total.",
                IsUnlocked: s => s.TotalCookiesBaked >= t)
            {
                // Intro tiers have bespoke names (id-derived overlay); the ramp
                // composes "<magnitude> baker" from an English magnitude phrase.
                NameOverlay = isIntro ? null : loc => loc.OverlayFormat("achievement.baker.name", phrase),
                DescOverlay = loc => loc.OverlayFormat("achievement.baked.desc", formatted),
            });
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
                var lowerName = building.Name.ToLowerInvariant();
                list.Add(new AchievementDefinition(
                    Id: $"own_{building.Id}_{count}",
                    Name: $"{count}× {lowerName}",
                    Icon: building.Icon,
                    Description: $"Own {count} {lowerName}{(count == 1 ? "" : "s")}.",
                    IsUnlocked: s => s.BuildingCounts.TryGetValue(bid, out var owned) && owned >= c)
                {
                    NameOverlay = loc => loc.OverlayFormat("achievement.own.name", c, Buildings.Get(bid).DisplayName(loc)),
                    DescOverlay = loc => loc.OverlayFormat("achievement.own.desc", c, Buildings.Get(bid).DisplayName(loc)),
                });
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
            var formatted = threshold.ToString("N0");
            list.Add(new AchievementDefinition(
                Id: $"clicks_{threshold}",
                Name: name,
                Icon: "👆",
                Description: $"Manually click the big cookie {formatted} times.",
                IsUnlocked: s => s.HandmadeClicks >= t)
            {
                DescOverlay = loc => loc.OverlayFormat("achievement.clicks.desc", formatted),
            });
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
            var isSmall = Math.Round(Math.Log10(threshold)) < 6;
            var phrase = MagnitudePhrase(threshold);
            var formatted = NumberFormat.Format(threshold);
            list.Add(new AchievementDefinition(
                Id: $"cps_{threshold:0}",
                Name: PerSecondName(threshold),
                Icon: "⏱️",
                Description: $"Reach {formatted} cookie{(threshold == 1 ? "" : "s")} per second.",
                IsUnlocked: s => s.CurrentCps() >= t)
            {
                // Small tiers (<1e6) have bespoke names (id-derived overlay); the
                // ramp composes "<magnitude> per second" from the magnitude phrase.
                NameOverlay = isSmall ? null : loc => loc.OverlayFormat("achievement.persecond.name", phrase),
                DescOverlay = loc => loc.OverlayFormat("achievement.cps.desc", formatted),
            });
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
            var formatted = threshold.ToString("N0");
            list.Add(new AchievementDefinition(
                Id: $"golden_{threshold}",
                Name: name,
                Icon: "🪙",
                Description: $"Click {formatted} golden cookies.",
                IsUnlocked: s => s.GoldenCookiesClicked >= t)
            {
                DescOverlay = loc => loc.OverlayFormat("achievement.golden.desc", formatted),
            });
        }

        // ---- Frenzy combo achievements (click a golden cookie during a frenzy) ----
        (long Threshold, string Name)[] comboMilestones =
        [
            (1,    "In the zone"),
            (10,   "Combo baker"),
            (50,   "Chain reaction"),
            (100,  "Frenzy virtuoso"),
        ];
        foreach (var (threshold, name) in comboMilestones)
        {
            var t = threshold;
            var formatted = threshold.ToString("N0");
            list.Add(new AchievementDefinition(
                Id: $"combo_{threshold}",
                Name: name,
                Icon: "⚡",
                Description: threshold == 1
                    ? "Click a golden cookie while a frenzy is active."
                    : $"Click {formatted} golden cookies while a frenzy is active.",
                IsUnlocked: s => s.GoldenClicksDuringFrenzy >= t)
            {
                DescOverlay = threshold == 1
                    ? loc => loc.Overlay("achievement.combo.desc_one")
                    : loc => loc.OverlayFormat("achievement.combo.desc", formatted),
            });
        }

        // ---- Handmade cookie achievements (cookies from manual clicks only) ----
        double[] handmadeThresholds =
            [1_000, 100_000, 1e7, 1e9, 1e11, 1e13, 1e15];
        foreach (var threshold in handmadeThresholds)
        {
            var t = threshold;
            var formatted = NumberFormat.Format(threshold);
            list.Add(new AchievementDefinition(
                Id: $"handmade_{threshold:0}",
                Name: HandmadeName(threshold),
                Icon: "🤲",
                Description: $"Make {formatted} cookies from manual clicks.",
                IsUnlocked: s => s.HandmadeCookies >= t)
            {
                DescOverlay = loc => loc.OverlayFormat("achievement.handmade.desc", formatted),
            });
        }

        // ---- Play-time achievements (uses the existing GameTime clock) ----
        (double Seconds, string Name)[] playtimeMilestones =
        [
            (3_600,     "Getting comfy"),        // 1 hour
            (36_000,    "Dedicated baker"),      // 10 hours
            (360_000,   "Marathon baker"),       // 100 hours
            (3_600_000, "Eternal shift"),        // 1000 hours
        ];
        foreach (var (seconds, name) in playtimeMilestones)
        {
            var sec = seconds;
            var hours = (int)(seconds / 3_600);
            var formatted = hours.ToString("N0");
            list.Add(new AchievementDefinition(
                Id: $"playtime_{(int)seconds}",
                Name: name,
                Icon: "🕰️",
                Description: $"Play for a total of {formatted} hour{(hours == 1 ? "" : "s")}.",
                IsUnlocked: s => s.GameTime >= sec)
            {
                DescOverlay = loc => loc.OverlayFormat("achievement.playtime.desc", formatted),
            });
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
            var formatted = threshold.ToString("N0");
            var isAll = threshold >= 83;
            list.Add(new AchievementDefinition(
                Id: $"upgrades_{threshold}",
                Name: name,
                Icon: threshold >= 83 ? "🏆" : threshold >= 50 ? "💎" : "✨",
                Description: isAll
                    ? "Own every upgrade in the game."
                    : $"Own {threshold} upgrades.",
                IsUnlocked: s => s.PurchasedUpgrades.Count >= t)
            {
                DescOverlay = isAll
                    ? loc => loc.Overlay("achievement.upgrades.desc_all")
                    : loc => loc.OverlayFormat("achievement.upgrades.desc", formatted),
            });
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
            var formatted = threshold.ToString("N0");
            list.Add(new AchievementDefinition(
                Id: $"sugar_{threshold}",
                Name: name,
                Icon: "🍬",
                Description: $"Harvest {formatted} sugar lump{(threshold == 1 ? "" : "s")}.",
                IsUnlocked: s => s.SugarLumps >= t)
            {
                DescOverlay = loc => loc.OverlayFormat("achievement.sugar.desc", formatted),
            });
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
            var formatted = threshold.ToString("N0");
            list.Add(new AchievementDefinition(
                Id: $"ascend_{threshold}",
                Name: name,
                Icon: "🔄",
                Description: $"Ascend {threshold} times.",
                IsUnlocked: s => s.AscensionCount >= t)
            {
                DescOverlay = loc => loc.OverlayFormat("achievement.ascend.desc", formatted),
            });
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
            var formatted = threshold.ToString("N0");
            list.Add(new AchievementDefinition(
                Id: $"prestige_{threshold}",
                Name: name,
                Icon: threshold >= 1_000 ? "🌌" : threshold >= 100 ? "🕊️" : "🌠",
                Description: $"Accumulate {formatted} prestige level{(threshold == 1 ? "" : "s")}.",
                IsUnlocked: s => s.PrestigeLevel >= t)
            {
                DescOverlay = loc => loc.OverlayFormat("achievement.prestige.desc", formatted),
            });
        }

        return list;
    }

    /// <summary>
    /// The English magnitude phrase for a power-of-ten threshold, e.g. 1e12 →
    /// "Trillion", 1e14 → "Hundred-trillion". Magnitude words stay English by
    /// design, so this phrase is shared by both the English source name and the
    /// Chinese overlay template (which only adds the localized suffix).
    /// </summary>
    private static string MagnitudePhrase(double threshold)
    {
        var exp = (int)Math.Round(Math.Log10(threshold));
        var tier = exp / 3;
        var remainder = exp % 3;
        var word = MagnitudeWords.TryGetValue(tier, out var w) ? w : "cosmic";
        var cap = char.ToUpperInvariant(word[0]) + word[1..];
        return remainder == 2 ? $"Hundred-{word}" : cap;
    }

    /// <summary>
    /// Builds an original "… baker" name from a power-of-ten threshold, e.g.
    /// 1e12 → "Trillion baker", 1e14 → "Hundred-trillion baker".
    /// </summary>
    private static string BakerName(double threshold) => $"{MagnitudePhrase(threshold)} baker";

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

        return $"{MagnitudePhrase(threshold)} per second";
    }

    /// <summary>
    /// Builds an original name for a handmade-cookie milestone, themed around
    /// hands / craftsmanship rather than raw magnitude.
    /// </summary>
    private static string HandmadeName(double threshold)
    {
        var exp = (int)Math.Round(Math.Log10(threshold));
        return exp switch
        {
            <= 3 => "Handcrafted",
            <= 5 => "Artisan baker",
            <= 7 => "Master kneader",
            <= 9 => "Callused fingertips",
            <= 11 => "Legendary hands",
            <= 13 => "Cookie sculptor",
            _ => "Bare-handed titan",
        };
    }
}
