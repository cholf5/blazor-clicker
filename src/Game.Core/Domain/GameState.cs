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
    /// <summary>
    /// Simulated <see cref="GameTime"/> (seconds) at which each entry in
    /// <see cref="PurchasedUpgrades"/> was bought. Used by the Stats dialog to
    /// offer a "recently purchased" ordering — the raw <see cref="HashSet{T}"/>
    /// enumeration order is undefined, so without this we couldn't answer
    /// "what did I just buy?". Cleared alongside <see cref="PurchasedUpgrades"/>
    /// on ascend, and stamped from a v5→v6 save migration for pre-existing
    /// entries.
    /// </summary>
    public Dictionary<string, double> UpgradePurchaseTimes { get; private set; } = new();

    // ---- World clock (seconds since state creation, monotonic) ----
    public double GameTime { get; private set; }

    // ---- Golden cookie & buffs ----
    public GoldenCookie? ActiveGolden { get; private set; }
    private double _nextGoldenAt;
    public List<ActiveBuff> Buffs { get; private set; } = new();

    // ---- Sugar lumps ----
    /// <summary>
    /// Unspent sugar lumps available to invest in building levels. (Prior to
    /// ADR 0006 this counted lumps as a permanent +1% global bonus; it is now a
    /// spendable balance — see the v4→v5 migration.)
    /// </summary>
    public long SugarLumps { get; private set; }
    /// <summary>Whether an unharvested sugar lump is currently ripe (waiting for the player to click).</summary>
    public bool SugarLumpReady { get; private set; }
    /// <summary>Game time (seconds) at which the next sugar lump will ripen. Used only when not already ripe.</summary>
    public double SugarLumpNextAt { get; private set; }
    /// <summary>
    /// Sugar-lump levels invested into each building. Each level adds +1% to that
    /// building's own production (see <see cref="GetBuildingUnitCps"/>). Levels are
    /// permanent meta progress — they survive ascension, like sugar lumps themselves.
    /// </summary>
    public Dictionary<BuildingId, int> BuildingLevels { get; private set; } = new();

    // ---- Prestige / ascension ----
    /// <summary>Number of heavenly / prestige levels this save has accumulated across every ascension.</summary>
    public int PrestigeLevel { get; private set; }

    // ---- Display preferences (persisted, but not part of the simulation) ----
    /// <summary>
    /// The language the player explicitly chose, or null to follow the system /
    /// browser language. Persisted so the choice survives reloads; the web layer
    /// reads it on startup and only auto-detects when it is null.
    /// </summary>
    public Localization.Language? ChosenLanguage { get; set; }

    // ---- Notifications (drained by the UI each frame) ----
    // Achievement notifications carry the achievement id; news messages carry a
    // translation key + args (see NewsMessage) so the UI localizes them at
    // display time and GameState stays free of any localizer dependency.
    private readonly Queue<string> _achievementNotifications = new();
    private readonly Queue<NewsMessage> _newsMessages = new();

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
        UpgradePurchaseTimes[upgradeId] = GameTime;
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
            _newsMessages.Enqueue(NewsMessage.Of("news.event.sugar_ripe"));
        }

        // 6. Check achievements
        CheckAchievements();
    }

    /// <summary>
    /// Catch up on real time that elapsed while the tab was merely backgrounded
    /// (not closed): the browser throttles background timers hard, so the game
    /// loop stops ticking at 30fps and, when it fires again, hands us one large
    /// gap. This credits that gap at <b>full</b> efficiency — the session never
    /// actually ended, so unlike <see cref="ApplyOfflineProgress"/> there is no
    /// 50% haircut and no minimum-report threshold.
    ///
    /// Why this is not just a big <see cref="Tick"/>:
    /// <list type="bullet">
    /// <item>Cookies are credited at <see cref="CurrentCpsRaw"/>, not
    /// <see cref="CurrentCps"/>, so a temporary Frenzy that is about to expire
    /// is not stretched across the whole away-window (same reasoning as offline
    /// progress).</item>
    /// <item>Golden cookies are not spawned/expired in a burst: the schedule is
    /// simply pushed forward, mirroring <see cref="ApplyOfflineProgress"/>.</item>
    /// </list>
    /// </summary>
    public void CatchUpProgress(double realSeconds)
    {
        if (realSeconds <= 0) return;

        // Credit cookies at the pre-buff rate captured *before* advancing the
        // clock (so buffs still active for this window are counted, but not
        // stretched past their expiry).
        var cps = CurrentCpsRaw();
        if (cps > 0)
        {
            var gained = cps * realSeconds;
            Cookies += gained;
            AddBaked(gained);
        }

        // Advance the world clock and expire anything that timed out while away.
        GameTime += realSeconds;
        Buffs.RemoveAll(b => b.ExpiresAt <= GameTime);

        // Ripen the pending sugar lump if its window passed.
        if (!SugarLumpReady && AllTimeCookiesBaked >= ProgressionConfig.SugarLumpUnlockThreshold
            && GameTime >= SugarLumpNextAt)
        {
            SugarLumpReady = true;
            _newsMessages.Enqueue(NewsMessage.Of("news.event.sugar_ripe"));
        }

        // Don't dump a burst of golden cookies for the missed time; just resume
        // the cadence shortly after we're back in the foreground.
        if (_nextGoldenAt < GameTime) _nextGoldenAt = GameTime + 15 + _random.NextDouble() * 60;

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
                _newsMessages.Enqueue(NewsMessage.Of("news.event.lucky", NumberFormat.Format(luckyGain)));
                break;
            case GoldenCookieEffect.Frenzy:
                Buffs.Add(new ActiveBuff(GoldenCookieEffect.Frenzy, 7.0, GameTime + 77));
                _newsMessages.Enqueue(NewsMessage.Of("news.event.frenzy"));
                break;
            case GoldenCookieEffect.ClickFrenzy:
                Buffs.Add(new ActiveBuff(GoldenCookieEffect.ClickFrenzy, 777.0, GameTime + 13));
                _newsMessages.Enqueue(NewsMessage.Of("news.event.click_frenzy"));
                break;
        }

        return effect;
    }

    /// <summary>Harvest a ripe sugar lump into the spendable balance. Returns true if one was actually harvested.</summary>
    public bool HarvestSugarLump()
    {
        if (!SugarLumpReady) return false;
        SugarLumpReady = false;
        SugarLumps++;
        SugarLumpNextAt = GameTime + ProgressionConfig.SugarLumpRipenSeconds;
        _newsMessages.Enqueue(NewsMessage.Of("news.event.harvest", SugarLumps));
        return true;
    }

    /// <summary>
    /// Whether the sugar-lump system has revealed itself to the player yet. Mirrors
    /// the widget's own visibility rule so the UI can decide, uniformly, when to
    /// surface sugar-lump info (e.g. a "Sugar level" row on every building tooltip,
    /// showing Lv 0 for un-invested ones) rather than only for buildings that
    /// happen to already have levels.
    /// </summary>
    public bool SugarLumpsUnlocked =>
        AllTimeCookiesBaked >= ProgressionConfig.SugarLumpUnlockThreshold
        || SugarLumps > 0
        || BuildingLevels.Count > 0;

    /// <summary>
    /// Sugar-lump cost to raise <paramref name="id"/> from its current level to the
    /// next one. Cost equals the target level (Cookie Clicker's triangular pricing:
    /// reaching level N costs 1+2+…+N lumps), so high levels get expensive fast —
    /// this, not the ripen time, is what bounds a single building's investment.
    /// </summary>
    public long BuildingLevelUpCost(BuildingId id) =>
        BuildingLevels.GetValueOrDefault(id) + 1;

    /// <summary>
    /// Spend sugar lumps to raise a building's level by one. Returns true on success,
    /// false if the balance can't cover <see cref="BuildingLevelUpCost"/>.
    /// </summary>
    public bool LevelUpBuilding(BuildingId id)
    {
        var cost = BuildingLevelUpCost(id);
        if (SugarLumps < cost) return false;
        SugarLumps -= cost;
        BuildingLevels[id] = BuildingLevels.GetValueOrDefault(id) + 1;
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
    /// sugar lumps (balance + invested building levels), prestige, and life-time counters.
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
        UpgradePurchaseTimes.Clear();
        Buffs.Clear();
        ActiveGolden = null;
        ScheduleNextGolden();

        var ascendKey = gained == 1 ? "news.event.ascend_one" : "news.event.ascend_many";
        _newsMessages.Enqueue(NewsMessage.Of(ascendKey, gained, PrestigeLevel));
        return true;
    }

    // =========================================================================
    // Debug / GM operations
    //
    // These bypass every cost check on purpose — they exist so a developer can
    // fast-forward to a state that's tedious to reach by hand (e.g. owning enough
    // Cursors to fill all five decorative rings). They live in the domain, not the
    // UI, because the "UI → domain, never the reverse" rule means the web layer
    // can't reach in and poke Cookies / BuildingCounts directly (those setters are
    // internal/private). The *only* caller is a Debug-only GM panel guarded by
    // `#if DEBUG`, so in a Release/GitHub-Pages build these methods compile but are
    // unreachable — no cheat surface ships to players. They stay outside `#if
    // DEBUG` here so the Release test run (dotnet test -c Release) can still cover
    // them.
    // =========================================================================

    /// <summary>Grant cookies outright, crediting the baked totals as if earned so
    /// milestone achievements and prestige math stay consistent. Debug/GM only.</summary>
    public void DebugAddCookies(double amount)
    {
        if (amount <= 0) return;
        Cookies += amount;
        AddBaked(amount);
    }

    /// <summary>Add copies of a building for free (no cost, no unlock gate).
    /// Negative or zero <paramref name="count"/> is a no-op. Debug/GM only.</summary>
    public void DebugAddBuilding(BuildingId id, int count)
    {
        if (count <= 0) return;
        BuildingCounts[id] = BuildingCounts.GetValueOrDefault(id) + count;
    }

    /// <summary>Grant every upgrade in the catalog for free, ignoring cost and
    /// unlock conditions. Already-owned upgrades are left as-is. Debug/GM only.</summary>
    public void DebugUnlockAllUpgrades()
    {
        foreach (var up in Upgrades.All)
        {
            if (PurchasedUpgrades.Add(up.Id))
                UpgradePurchaseTimes[up.Id] = GameTime;
        }
    }

    /// <summary>Add to the spendable sugar-lump balance for free. Negative or zero
    /// is a no-op. Debug/GM only.</summary>
    public void DebugAddSugarLumps(long count)
    {
        if (count <= 0) return;
        SugarLumps += count;
    }

    /// <summary>Drain queued achievement notifications for display.</summary>
    public IReadOnlyList<string> DrainAchievementNotifications()
    {
        if (_achievementNotifications.Count == 0) return Array.Empty<string>();
        var result = _achievementNotifications.ToArray();
        _achievementNotifications.Clear();
        return result;
    }

    /// <summary>Drain queued news-ticker flavor messages (translation key + args) for display.</summary>
    public IReadOnlyList<NewsMessage> DrainNewsMessages()
    {
        if (_newsMessages.Count == 0) return Array.Empty<NewsMessage>();
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
        var milk = MilkFactor();
        foreach (var upId in PurchasedUpgrades)
        {
            if (!Upgrades.Exists(upId)) continue;
            var up = Upgrades.Get(upId);
            if (up.EffectKind == UpgradeEffectKind.GlobalCpsMultiplier)
                globalMult *= up.EffectValue;
            // Kitten upgrades convert milk into a multiplier: the more
            // achievements unlocked, the stronger each kitten tier becomes.
            else if (up.EffectKind == UpgradeEffectKind.KittenMilkMultiplier)
                globalMult *= 1 + milk * up.EffectValue;
        }

        // Prestige stacks additively as a "% bonus", then applies multiplicatively
        // to the whole economy so late-game growth remains large. Sugar lumps are
        // deliberately NOT here: per ADR 0006 they boost a single building each
        // (see GetBuildingUnitCps), which keeps their aggregate magnitude tiny.
        var permanentBonus = 1.0
            + PrestigeLevel * ProgressionConfig.PrestigeCpsBonus;

        return total * globalMult * permanentBonus;
    }

    /// <summary>
    /// Current milk amount as a fraction (1.0 == 100%), derived purely from the
    /// number of achievements unlocked. Milk is not stored — it is recomputed on
    /// demand like <see cref="CurrentCps"/>, so it needs no save migration and no
    /// backfill. Kitten upgrades (<see cref="UpgradeEffectKind.KittenMilkMultiplier"/>)
    /// read this to scale their global CPS bonus.
    /// </summary>
    public double MilkFactor() =>
        UnlockedAchievements.Count * ProgressionConfig.MilkPerAchievement;

    /// <summary>
    /// The combined global CPS multiplier that milk currently contributes through
    /// every purchased kitten upgrade (1.0 == no bonus). Mirrors the kitten branch
    /// of <see cref="CurrentCpsRaw"/> exactly so the UI can show milk's real,
    /// aggregate effect on production without duplicating the stacking rule.
    /// Returns 1.0 when no kitten upgrades are owned (milk then does nothing yet).
    /// </summary>
    public double MilkCpsMultiplier()
    {
        var milk = MilkFactor();
        var mult = 1.0;
        foreach (var upId in PurchasedUpgrades)
        {
            if (!Upgrades.Exists(upId)) continue;
            var up = Upgrades.Get(upId);
            if (up.EffectKind == UpgradeEffectKind.KittenMilkMultiplier)
                mult *= 1 + milk * up.EffectValue;
        }
        return mult;
    }

    /// <summary>CPS contributed by all copies of the given building, incl. its upgrades.</summary>
    public double GetBuildingTotalCps(BuildingId id) =>
        GetBuildingUnitCps(id) * BuildingCounts.GetValueOrDefault(id);

    /// <summary>
    /// Fraction (0..1) of total production this building is responsible for.
    /// Compared against the <b>raw building sum</b> — not <see cref="CurrentCps"/> —
    /// on purpose: global multipliers, prestige, sugar lumps and temporary Frenzy
    /// buffs all scale every building uniformly, so they cancel in the ratio.
    /// Dividing a single building's un-buffed CPS by the buff-inflated
    /// <see cref="CurrentCps"/> would understate its share (e.g. by 7× during a
    /// x7 Frenzy). Returns 0 when nothing is producing.
    /// </summary>
    public double BuildingCpsShare(BuildingId id)
    {
        double buildingSum = 0;
        foreach (var b in Buildings.All)
            buildingSum += GetBuildingTotalCps(b.Id);

        return buildingSum > 0 ? GetBuildingTotalCps(id) / buildingSum : 0;
    }

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

        // Sugar-lump levels invested in this building add +1% each, additively,
        // and only to this building (ADR 0006). This is why sugar lumps stay a
        // flavour bonus rather than a growth axis.
        var level = BuildingLevels.GetValueOrDefault(id);
        if (level > 0)
            mult *= 1.0 + level * ProgressionConfig.SugarLumpBuildingLevelBonus;

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

    /// <summary>
    /// Whether a building is revealed to the player. This is a <b>presentation</b>
    /// signal: the shop shows unlocked buildings and hides/greys the rest — it is
    /// deliberately <b>not</b> enforced inside <see cref="BuyBuilding"/>, mirroring
    /// the original where progressive reveal is a display behaviour, not an
    /// economic constraint (during normal play the conditions are always met by
    /// the time you can afford one). Computed on demand — never persisted — from
    /// two signals that only move forward within a run, so an unlock can never be
    /// undone: the previous building being owned, and this run's TotalCookiesBaked
    /// crossing the building's threshold. The first building has no predecessor
    /// and is always unlocked. See ProgressionConfig.BuildingUnlockCostFraction.
    /// </summary>
    public bool IsBuildingUnlocked(BuildingId id)
    {
        var all = Buildings.All;
        var index = -1;
        for (var i = 0; i < all.Count; i++)
        {
            if (all[i].Id == id) { index = i; break; }
        }
        // An id not present in the catalog is a programming error, not a runtime
        // state we should paper over by pretending it's unlocked.
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(id), id, "Building id is not in the catalog.");
        if (index == 0) return true; // Cursor has no predecessor — always unlocked.

        var prerequisiteOwned = BuildingCounts.GetValueOrDefault(all[index - 1].Id) >= 1;
        return prerequisiteOwned && TotalCookiesBaked >= BuildingUnlockThreshold(id);
    }

    /// <summary>
    /// This run's TotalCookiesBaked at which <paramref name="id"/> reveals in the
    /// shop. Single source of truth for the unlock formula — both the domain's
    /// <see cref="IsBuildingUnlocked"/> gate and the UI's "Unlocks at N baked"
    /// hint read this, so the number shown can never drift from the number
    /// actually used. See ProgressionConfig.BuildingUnlockCostFraction.
    /// </summary>
    public double BuildingUnlockThreshold(BuildingId id) =>
        Buildings.Get(id).BaseCost * ProgressionConfig.BuildingUnlockCostFraction;

    /// <summary>
    /// The first not-yet-unlocked building in catalog order, used to render a
    /// single mysterious "coming next" placeholder in the shop. Returns null
    /// once every building is unlocked.
    /// </summary>
    public BuildingId? NextLockedBuilding()
    {
        foreach (var def in Buildings.All)
        {
            if (!IsBuildingUnlocked(def.Id)) return def.Id;
        }
        return null;
    }

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

        _newsMessages.Enqueue(NewsMessage.Of("news.event.golden_spawn"));
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
                // The news line references the achievement by id; the UI resolves
                // its localized name when it drains and renders the message.
                _newsMessages.Enqueue(NewsMessage.Of("news.event.achievement", ach.Id));
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
        UpgradePurchaseTimes = data.UpgradePurchaseTimes.ToDictionary(k => k.Key, k => k.Value);
        UnlockedAchievements = new HashSet<string>(data.UnlockedAchievements);
        Buffs = data.Buffs.ToList();
        ActiveGolden = data.ActiveGolden;
        _nextGoldenAt = data.NextGoldenAt;
        SugarLumps = data.SugarLumps;
        SugarLumpReady = data.SugarLumpReady;
        SugarLumpNextAt = data.SugarLumpNextAt > 0
            ? data.SugarLumpNextAt
            : GameTime + ProgressionConfig.SugarLumpRipenSeconds;
        BuildingLevels = data.BuildingLevels.ToDictionary(k => k.Key, k => k.Value);
        PrestigeLevel = data.PrestigeLevel;
        ChosenLanguage = data.Language;
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
        UpgradePurchaseTimes = UpgradePurchaseTimes.ToDictionary(k => k.Key, k => k.Value),
        UnlockedAchievements = UnlockedAchievements.ToList(),
        Buffs = Buffs.ToList(),
        ActiveGolden = ActiveGolden,
        NextGoldenAt = _nextGoldenAt,
        SugarLumps = SugarLumps,
        SugarLumpReady = SugarLumpReady,
        SugarLumpNextAt = SugarLumpNextAt,
        BuildingLevels = BuildingLevels.ToDictionary(k => k.Key, k => k.Value),
        PrestigeLevel = PrestigeLevel,
        Language = ChosenLanguage,
    };
}
