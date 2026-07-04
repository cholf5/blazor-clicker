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
    public double TotalCookiesBaked { get; private set; }

    // ---- Counters ----
    public long HandmadeClicks { get; private set; }
    public long GoldenCookiesClicked { get; private set; }

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

    // ---- Notifications (drained by the UI each frame) ----
    private readonly Queue<string> _achievementNotifications = new();

    public GameState() : this(new Random()) { }

    public GameState(Random random)
    {
        _random = random;
        // Schedule the first golden cookie 60-90 seconds into the run.
        _nextGoldenAt = _random.NextDouble() * 30 + 60;
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
        TotalCookiesBaked += gained;
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
            TotalCookiesBaked += gained;
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

        // 5. Check achievements
        CheckAchievements();
    }

    /// <summary>Consume the currently visible golden cookie. Returns the effect it produced.</summary>
    public GoldenCookieEffect? ClickGoldenCookie()
    {
        if (ActiveGolden is not { } g) return null;

        var effect = g.Effect;
        GoldenCookiesClicked++;
        ActiveGolden = null;
        ScheduleNextGolden();

        switch (effect)
        {
            case GoldenCookieEffect.Lucky:
                // min(15% of bank, 15 minutes of CPS) + 13 cookies
                var luckyGain = Math.Min(Cookies * 0.15, CurrentCpsRaw() * 60 * 15) + 13;
                if (luckyGain < 13) luckyGain = 13; // sanity floor
                Cookies += luckyGain;
                TotalCookiesBaked += luckyGain;
                break;
            case GoldenCookieEffect.Frenzy:
                Buffs.Add(new ActiveBuff(GoldenCookieEffect.Frenzy, 7.0, GameTime + 77));
                break;
            case GoldenCookieEffect.ClickFrenzy:
                Buffs.Add(new ActiveBuff(GoldenCookieEffect.ClickFrenzy, 777.0, GameTime + 13));
                break;
        }

        return effect;
    }

    /// <summary>Drain queued achievement notifications for display.</summary>
    public IReadOnlyList<string> DrainAchievementNotifications()
    {
        if (_achievementNotifications.Count == 0) return Array.Empty<string>();
        var result = _achievementNotifications.ToArray();
        _achievementNotifications.Clear();
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
        return total * globalMult;
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
    }

    private void ScheduleNextGolden()
    {
        // 60-300 seconds until next spawn attempt.
        _nextGoldenAt = GameTime + 60 + _random.NextDouble() * 240;
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
        HandmadeClicks = data.HandmadeClicks;
        GoldenCookiesClicked = data.GoldenCookiesClicked;
        GameTime = data.GameTime;
        BuildingCounts = data.BuildingCounts.ToDictionary(k => k.Key, k => k.Value);
        PurchasedUpgrades = new HashSet<string>(data.PurchasedUpgrades);
        UnlockedAchievements = new HashSet<string>(data.UnlockedAchievements);
        Buffs = data.Buffs.ToList();
        ActiveGolden = data.ActiveGolden;
        _nextGoldenAt = data.NextGoldenAt;
    }

    internal SaveData ToSaveData() => new()
    {
        Cookies = Cookies,
        TotalCookiesBaked = TotalCookiesBaked,
        HandmadeClicks = HandmadeClicks,
        GoldenCookiesClicked = GoldenCookiesClicked,
        GameTime = GameTime,
        BuildingCounts = BuildingCounts.ToDictionary(k => k.Key, k => k.Value),
        PurchasedUpgrades = PurchasedUpgrades.ToList(),
        UnlockedAchievements = UnlockedAchievements.ToList(),
        Buffs = Buffs.ToList(),
        ActiveGolden = ActiveGolden,
        NextGoldenAt = _nextGoldenAt,
    };
}
