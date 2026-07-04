using Game.Core.Data;

namespace Game.Core.Domain;

/// <summary>
/// Aggregate root for the entire game. Contains all mutable state and the
/// operations that transition it. Pure logic — zero UI, zero DOM, no JS interop.
///
/// The UI layer polls <see cref="GameState"/> methods and properties every
/// frame; only <see cref="Tick"/> advances the simulation clock.
/// </summary>
public sealed class GameState
{
    private readonly Random _random;

    // ---- Core resources ----
    public double Cookies { get; internal set; }
    /// <summary>Cookies baked in the current run (resets on ascend).</summary>
    public double TotalCookiesBaked { get; private set; }
    /// <summary>Cookies baked across every run this save has ever done. Never resets.</summary>
    public double AllTimeCookiesBaked { get; private set; }

    // ---- Counters ----
    public long HandmadeClicks { get; private set; }
    public long GoldenCookiesClicked { get; private set; }
    /// <summary>Cookies produced by manual clicks only (never resets, incl. click buffs).</summary>
    public double HandmadeCookies { get; private set; }
    /// <summary>Golden cookies clicked while a Frenzy or Click frenzy buff was already active.</summary>
    public long GoldenClicksDuringFrenzy { get; private set; }
    /// <summary>Number of times the player has ascended (reset for prestige).</summary>
    public int AscensionCount { get; private set; }

    // ---- Ownership ----
    public Dictionary<BuildingId, int> BuildingCounts { get; private set; } = new();
    public HashSet<string> PurchasedUpgrades { get; private set; } = new();
    public HashSet<string> UnlockedAchievements { get; private set; } = new();

    // ---- World clock (seconds since state creation, monotonic) ----
    public double GameTime { get; private set; }

    // ---- Golden cookie & buffs ----
    public GoldenCookie? ActiveGolden { get; private set; }
    private double _nextGoldenAt;
    public List<ActiveBuff> Buffs { get; private set; } = new();

    // ---- Sugar lumps ----
    /// <summary>Total sugar lumps harvested over the lifetime of this save.</summary>
    public long SugarLumps { get; private set; }
    /// <summary>Whether an unharvested sugar lump is currently ripe (waiting for the player to click).</summary>
    public bool SugarLumpReady { get; private set; }
    /// <summary>Game time (seconds) at which the next sugar lump will ripen. Used only when not already ripe.</summary>
    public double SugarLumpNextAt { get; private set; }

    // ---- Prestige / ascension ----
    /// <summary>Number of heavenly / prestige levels this save has accumulated across every ascension.</summary>
    public int PrestigeLevel { get; private set; }

    // ---- Notifications (drained by the UI each frame) ----
    private readonly Queue<string> _achievementNotifications = new();
    private readonly Queue<string> _newsMessages = new();

    public GameState() : this(new Random()) { }

    public GameState(Random random)
    {
        _random = random;
        // Schedule the first golden cookie 60-90 seconds into the run.
        _nextGoldenAt = _random.NextDouble() * 30 + 60;
        SugarLumpNextAt = ProgressionConfig.SugarLumpRipenSeconds;
    }

    // =========================================================================
    // Actions
    // =========================================================================

    /// <summary>
    /// Handle a manual click on the Big Cookie. Returns the amount produced.
    /// </summary>
    public double Click()
    {
        var gained = ClickPower();
        Cookies += gained;
        AddBaked(gained);
        HandmadeCookies += gained;
        HandmadeClicks++;
        return gained;
    }

    /// <summary>Try to purchase one copy of a building. Returns true on success.</summary>
    public bool BuyBuilding(BuildingId id)
    {
        var owned = BuildingCounts.GetValueOrDefault(id);
        var cost = Formulas.BuildingCost(Buildings.Get(id).BaseCost, owned);
        if (Cookies < cost) return false;
        Cookies -= cost;
        BuildingCounts[id] = owned + 1;
        return true;
    }

    /// <summary>Try to purchase multiple copies of a building at once.</summary>
    public int BuyBuildingBulk(BuildingId id, int amount)
    {
        var bought = 0;
        for (var i = 0; i < amount; i++)
        {
            if (!BuyBuilding(id)) break;
            bought++;
        }
        return bought;
    }

    /// <summary>Try to purchase a specific upgrade. Returns true on success.</summary>
    public bool BuyUpgrade(string upgradeId)
    {
        if (PurchasedUpgrades.Contains(upgradeId)) return false;
        if (!Upgrades.Exists(upgradeId)) return false;
        var def = Upgrades.Get(upgradeId);
        if (!def.IsUnlocked(this)) return false;
        if (Cookies < def.Cost) return false;

        Cookies -= def.Cost;
        PurchasedUpgrades.Add(upgradeId);
        return true;
    }

    /// <summary>
    /// Advance the game clock by <paramref name="deltaSeconds"/>. Applies passive
    /// CPS, expires buffs, potentially spawns a golden cookie, and checks
    /// achievements.
    /// </summary>
    public void Tick(double deltaSeconds)
    {
        if (deltaSeconds <= 0) return;

        GameTime += deltaSeconds;

        // 1. Passive cookies from CPS
        var cps = CurrentCps();
        if (cps > 0)
        {
            var gained = cps * deltaSeconds;
            Cookies += gained;
            AddBaked(gained);
        }

        // 2. Expire buffs
        Buffs.RemoveAll(b => b.ExpiresAt <= GameTime);

        // 3. Despawn the active golden cookie if it timed out
        if (ActiveGolden is { } g && GameTime >= g.ExpiresAt)
        {
            ActiveGolden = null;
            ScheduleNextGolden();
        }

        // 4. Maybe spawn one
        if (ActiveGolden is null && GameTime >= _nextGoldenAt)
        {
            SpawnGoldenCookie();
        }

        // 5. Sugar lump ripens (only after the unlock threshold has been hit)
        if (!SugarLumpReady && AllTimeCookiesBaked >= ProgressionConfig.SugarLumpUnlockThreshold
            && GameTime >= SugarLumpNextAt)
        {
            SugarLumpReady = true;
            _newsMessages.Enqueue("A sugar lump is ripe — go harvest it!");
        }

        // 6. Check achievements
        CheckAchievements();
    }

    /// <summary>
    /// Simulate a period the player spent away from the tab. Grants a
    /// reduced-efficiency cookie yield and advances timers.
    /// </summary>
    public OfflineEarningsSummary ApplyOfflineProgress(double realSeconds)
    {
        if (realSeconds < ProgressionConfig.OfflineMinReportSeconds)
            return new OfflineEarningsSummary(realSeconds, 0, ProgressionConfig.OfflineEfficiency, false);

        var capped = Math.Min(realSeconds, ProgressionConfig.OfflineMaxSeconds);

        // Advance the world clock so buffs expire and sugar lumps ripen naturally.
        GameTime += capped;
        Buffs.RemoveAll(b => b.ExpiresAt <= GameTime);

        // Cookies granted at reduced efficiency. Use CurrentCpsRaw so temporary
        // buffs don't inflate offline earnings.
        var cps = CurrentCpsRaw();
        var earned = cps * capped * ProgressionConfig.OfflineEfficiency;
        if (earned > 0)
        {
            Cookies += earned;
            AddBaked(earned);
        }

        var lumpNow = false;
        if (!SugarLumpReady && AllTimeCookiesBaked >= ProgressionConfig.SugarLumpUnlockThreshold
            && GameTime >= SugarLumpNextAt)
        {
            SugarLumpReady = true;
            lumpNow = true;
        }

        // Push forward the next-golden schedule so we don't spawn 30 golden cookies at once
        // after coming back from an 8-hour break.
        if (_nextGoldenAt < GameTime) _nextGoldenAt = GameTime + 15 + _random.NextDouble() * 60;

        CheckAchievements();

        return new OfflineEarningsSummary(capped, earned, ProgressionConfig.OfflineEfficiency, lumpNow);
    }

    /// <summary>Consume the currently visible golden cookie. Returns the effect it produced.</summary>
    public GoldenCookieEffect? ClickGoldenCookie()
    {
        if (ActiveGolden is not { } g) return null;

        var effect = g.Effect;
        GoldenCookiesClicked++;
        // Count this as a combo click if a frenzy-type buff was already running
        // when the cookie was clicked (checked before this click adds its own).
        if (Buffs.Any(b => b.Effect is GoldenCookieEffect.Frenzy or GoldenCookieEffect.ClickFrenzy))
            GoldenClicksDuringFrenzy++;
        ActiveGolden = null;
        ScheduleNextGolden();

        switch (effect)
        {
            case GoldenCookieEffect.Lucky:
                // min(15% of bank, 15 minutes of CPS) + 13 cookies
                var luckyGain = Math.Min(Cookies * 0.15, CurrentCpsRaw() * 60 * 15) + 13;
                if (luckyGain < 13) luckyGain = 13; // sanity floor
                Cookies += luckyGain;
                AddBaked(luckyGain);
                _newsMessages.Enqueue($"Lucky! +{luckyGain:N0} cookies.");
                break;
            case GoldenCookieEffect.Frenzy:
                Buffs.Add(new ActiveBuff(GoldenCookieEffect.Frenzy, 7.0, GameTime + 77));
                _newsMessages.Enqueue("Frenzy! CPS ×7 for 77 seconds.");
                break;
            case GoldenCookieEffect.ClickFrenzy:
                Buffs.Add(new ActiveBuff(GoldenCookieEffect.ClickFrenzy, 777.0, GameTime + 13));
                _newsMessages.Enqueue("Click frenzy! Click power ×777 for 13 seconds.");
                break;
        }

        return effect;
    }

    /// <summary>Harvest a ripe sugar lump. Returns true if one was actually harvested.</summary>
    public bool HarvestSugarLump()
    {
        if (!SugarLumpReady) return false;
        SugarLumpReady = false;
        SugarLumps++;
        SugarLumpNextAt = GameTime + ProgressionConfig.SugarLumpRipenSeconds;
        _newsMessages.Enqueue($"Harvested a sugar lump! You now have {SugarLumps}.");
        return true;
    }

    /// <summary>
    /// Prestige levels this run would grant on ascend. Zero if the player
    /// hasn't crossed <see cref="ProgressionConfig.MinCookiesToAscend"/>.
    /// </summary>
    public int PrestigeAvailableFromAscend()
    {
        if (TotalCookiesBaked < ProgressionConfig.MinCookiesToAscend) return 0;
        return (int)Math.Floor(Math.Cbrt(TotalCookiesBaked / ProgressionConfig.PrestigeCubeUnit));
    }

    /// <summary>
    /// Reset the current run in exchange for prestige levels. Keeps achievements,
    /// sugar lumps, prestige, and life-time counters.
    /// </summary>
    public bool Ascend()
    {
        var gained = PrestigeAvailableFromAscend();
        if (gained <= 0) return false;

        PrestigeLevel += gained;
        AscensionCount++;

        // Reset the run — keep meta progress (achievements, sugar lumps, prestige).
        Cookies = 0;
        TotalCookiesBaked = 0;
        HandmadeClicks = 0;
        BuildingCounts.Clear();
        PurchasedUpgrades.Clear();
        Buffs.Clear();
        ActiveGolden = null;
        ScheduleNextGolden();

        _newsMessages.Enqueue($"Ascended! Gained {gained} prestige level{(gained == 1 ? "" : "s")}. New total: {PrestigeLevel}.");
        return true;
    }

    /// <summary>Drain queued achievement notifications for display.</summary>
    public IReadOnlyList<string> DrainAchievementNotifications()
    {
        if (_achievementNotifications.Count == 0) return Array.Empty<string>();
        var result = _achievementNotifications.ToArray();
        _achievementNotifications.Clear();
        return result;
    }

    /// <summary>Drain queued news-ticker flavor messages for display.</summary>
    public IReadOnlyList<string> DrainNewsMessages()
    {
        if (_newsMessages.Count == 0) return Array.Empty<string>();
        var result = _newsMessages.ToArray();
        _newsMessages.Clear();
        return result;
    }

    // =========================================================================
    // Queries
    // =========================================================================

    /// <summary>Current cookies-per-second including buffs.</summary>
    public double CurrentCps()
    {
        var raw = CurrentCpsRaw();
        var frenzy = Buffs.Where(b => b.Effect == GoldenCookieEffect.Frenzy).Sum(b => b.Multiplier);
        var frenzyMult = frenzy > 0 ? frenzy : 1.0;
        return raw * frenzyMult;
    }

    /// <summary>CPS before temporary buffs — used inside Lucky calculation.</summary>
    public double CurrentCpsRaw()
    {
        double total = 0;
        foreach (var b in Buildings.All)
            total += GetBuildingTotalCps(b.Id);

        // Global CPS multipliers stack multiplicatively.
        var globalMult = 1.0;
        foreach (var upId in PurchasedUpgrades)
        {
            if (!Upgrades.Exists(upId)) continue;
            var up = Upgrades.Get(upId);
            if (up.EffectKind == UpgradeEffectKind.GlobalCpsMultiplier)
                globalMult *= up.EffectValue;
        }

        // Prestige & sugar lumps stack additively as a "% bonus", then apply
        // multiplicatively to the whole economy so late-game growth remains large.
        var permanentBonus = 1.0
            + PrestigeLevel * ProgressionConfig.PrestigeCpsBonus
            + SugarLumps * ProgressionConfig.SugarLumpCpsBonus;

        return total * globalMult * permanentBonus;
    }

    /// <summary>CPS contributed by all copies of the given building, incl. its upgrades.</summary>
    public double GetBuildingTotalCps(BuildingId id) =>
        GetBuildingUnitCps(id) * BuildingCounts.GetValueOrDefault(id);

    /// <summary>CPS a single copy of this building contributes, incl. tier upgrades + cursor synergy.</summary>
    public double GetBuildingUnitCps(BuildingId id)
    {
        var def = Buildings.Get(id);
        var baseCps = def.BaseCps;

        // Cursor gets a flat "+X per non-cursor building" bonus from synergy upgrades.
        if (id == BuildingId.Cursor)
        {
            double perNonCursor = 0;
            foreach (var upId in PurchasedUpgrades)
            {
                if (!Upgrades.Exists(upId)) continue;
                var up = Upgrades.Get(upId);
                if (up.EffectKind == UpgradeEffectKind.CursorPerNonCursorBuilding)
                    perNonCursor += up.EffectValue;
            }
            var nonCursorCount = 0;
            foreach (var (bid, count) in BuildingCounts)
                if (bid != BuildingId.Cursor) nonCursorCount += count;
            baseCps += perNonCursor * nonCursorCount;
        }

        var mult = 1.0;
        foreach (var upId in PurchasedUpgrades)
        {
            if (!Upgrades.Exists(upId)) continue;
            var up = Upgrades.Get(upId);
            if (up.EffectKind == UpgradeEffectKind.BuildingMultiplier && up.TargetBuilding == id)
                mult *= up.EffectValue;
        }

        return baseCps * mult;
    }

    /// <summary>Cookies produced by one manual click, incl. click multipliers and buffs.</summary>
    public double ClickPower()
    {
        var power = 1.0;
        foreach (var upId in PurchasedUpgrades)
        {
            if (!Upgrades.Exists(upId)) continue;
            var up = Upgrades.Get(upId);
            if (up.EffectKind == UpgradeEffectKind.ClickMultiplier)
                power *= up.EffectValue;
        }

        // 1% of CPS per click as passive bonus, echoing original CC.
        power += CurrentCps() * 0.01;

        var clickFrenzy = Buffs.Where(b => b.Effect == GoldenCookieEffect.ClickFrenzy).Sum(b => b.Multiplier);
        var clickMult = clickFrenzy > 0 ? clickFrenzy : 1.0;
        return power * clickMult;
    }

    /// <summary>Cost of buying one more of this building.</summary>
    public double NextBuildingCost(BuildingId id) =>
        Formulas.BuildingCost(Buildings.Get(id).BaseCost, BuildingCounts.GetValueOrDefault(id));

    /// <summary>Cost of buying <paramref name="amount"/> more of this building.</summary>
    public double BulkBuildingCost(BuildingId id, int amount) =>
        Formulas.BulkBuildingCost(Buildings.Get(id).BaseCost, BuildingCounts.GetValueOrDefault(id), amount);

    /// <summary>Upgrades currently for sale (unlocked and not yet purchased), cheapest first.</summary>
    public IEnumerable<UpgradeDefinition> AvailableUpgrades() =>
        Upgrades.All
            .Where(u => !PurchasedUpgrades.Contains(u.Id) && u.IsUnlocked(this))
            .OrderBy(u => u.Cost);

    // =========================================================================
    // Golden cookie helpers
    // =========================================================================

    private void SpawnGoldenCookie()
    {
        var pick = _random.NextDouble();
        // 50% Lucky, 35% Frenzy, 15% Click frenzy
        var effect = pick < 0.50 ? GoldenCookieEffect.Lucky
                   : pick < 0.85 ? GoldenCookieEffect.Frenzy
                                 : GoldenCookieEffect.ClickFrenzy;

        ActiveGolden = new GoldenCookie(
            Id: Guid.NewGuid(),
            Effect: effect,
            SpawnedAt: GameTime,
            ExpiresAt: GameTime + 13,
            ScreenX: 0.1 + _random.NextDouble() * 0.8,
            ScreenY: 0.15 + _random.NextDouble() * 0.7);

        _newsMessages.Enqueue("A golden cookie is glinting somewhere on screen…");
    }

    private void ScheduleNextGolden()
    {
        // 60-300 seconds until next spawn attempt.
        _nextGoldenAt = GameTime + 60 + _random.NextDouble() * 240;
    }

    private void AddBaked(double amount)
    {
        TotalCookiesBaked += amount;
        AllTimeCookiesBaked += amount;
    }

    // =========================================================================
    // Achievements
    // =========================================================================

    private void CheckAchievements()
    {
        foreach (var ach in Achievements.All)
        {
            if (UnlockedAchievements.Contains(ach.Id)) continue;
            if (ach.IsUnlocked(this))
            {
                UnlockedAchievements.Add(ach.Id);
                _achievementNotifications.Enqueue(ach.Id);
                _newsMessages.Enqueue($"Achievement unlocked: {ach.Name}!");
            }
        }
    }

    // =========================================================================
    // Save / Load
    // =========================================================================

    internal void ApplyLoaded(SaveData data)
    {
        Cookies = data.Cookies;
        TotalCookiesBaked = data.TotalCookiesBaked;
        AllTimeCookiesBaked = data.AllTimeCookiesBaked > 0 ? data.AllTimeCookiesBaked : data.TotalCookiesBaked;
        HandmadeClicks = data.HandmadeClicks;
        GoldenCookiesClicked = data.GoldenCookiesClicked;
        HandmadeCookies = data.HandmadeCookies;
        GoldenClicksDuringFrenzy = data.GoldenClicksDuringFrenzy;
        AscensionCount = data.AscensionCount;
        GameTime = data.GameTime;
        BuildingCounts = data.BuildingCounts.ToDictionary(k => k.Key, k => k.Value);
        PurchasedUpgrades = new HashSet<string>(data.PurchasedUpgrades);
        UnlockedAchievements = new HashSet<string>(data.UnlockedAchievements);
        Buffs = data.Buffs.ToList();
        ActiveGolden = data.ActiveGolden;
        _nextGoldenAt = data.NextGoldenAt;
        SugarLumps = data.SugarLumps;
        SugarLumpReady = data.SugarLumpReady;
        SugarLumpNextAt = data.SugarLumpNextAt > 0
            ? data.SugarLumpNextAt
            : GameTime + ProgressionConfig.SugarLumpRipenSeconds;
        PrestigeLevel = data.PrestigeLevel;
    }

    internal SaveData ToSaveData() => new()
    {
        Cookies = Cookies,
        TotalCookiesBaked = TotalCookiesBaked,
        AllTimeCookiesBaked = AllTimeCookiesBaked,
        HandmadeClicks = HandmadeClicks,
        GoldenCookiesClicked = GoldenCookiesClicked,
        HandmadeCookies = HandmadeCookies,
        GoldenClicksDuringFrenzy = GoldenClicksDuringFrenzy,
        AscensionCount = AscensionCount,
        GameTime = GameTime,
        BuildingCounts = BuildingCounts.ToDictionary(k => k.Key, k => k.Value),
        PurchasedUpgrades = PurchasedUpgrades.ToList(),
        UnlockedAchievements = UnlockedAchievements.ToList(),
        Buffs = Buffs.ToList(),
        ActiveGolden = ActiveGolden,
        NextGoldenAt = _nextGoldenAt,
        SugarLumps = SugarLumps,
        SugarLumpReady = SugarLumpReady,
        SugarLumpNextAt = SugarLumpNextAt,
        PrestigeLevel = PrestigeLevel,
    };
}
