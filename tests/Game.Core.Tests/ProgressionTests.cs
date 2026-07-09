using Game.Core.Domain;
using Game.Core.Data;
using Game.Core.Localization;

namespace Game.Core.Tests;

public class ProgressionTests
{
    // ------------------------------------------------------------------
    // Sugar lumps
    // ------------------------------------------------------------------

    [Fact]
    public void SugarLump_DoesNotRipenBeforeUnlockThreshold()
    {
        var s = new GameState();
        // Advance simulated time well past the ripen window, but keep
        // AllTimeCookiesBaked below the unlock threshold.
        s.Tick(ProgressionConfig.SugarLumpRipenSeconds + 60);
        Assert.False(s.SugarLumpReady);
        Assert.Equal(0, s.SugarLumps);
    }

    [Fact]
    public void SugarLump_RipensAfterThresholdAndWindow()
    {
        var s = new GameState();
        // Seed the lifetime baked counter directly — the intended path is a
        // very long simulated tick, which is fine for testing purposes.
        typeof(GameState).GetProperty("AllTimeCookiesBaked")!
            .SetValue(s, ProgressionConfig.SugarLumpUnlockThreshold + 10);
        s.Tick(ProgressionConfig.SugarLumpRipenSeconds + 1);
        Assert.True(s.SugarLumpReady);
    }

    [Fact]
    public void SugarLump_HarvestClearsReadyAndReschedules()
    {
        var s = new GameState();
        typeof(GameState).GetProperty("AllTimeCookiesBaked")!
            .SetValue(s, ProgressionConfig.SugarLumpUnlockThreshold + 10);
        s.Tick(ProgressionConfig.SugarLumpRipenSeconds + 1);
        Assert.True(s.SugarLumpReady);

        Assert.True(s.HarvestSugarLump());
        Assert.False(s.SugarLumpReady);
        Assert.Equal(1, s.SugarLumps);
        // Next ripen scheduled into the future.
        Assert.True(s.SugarLumpNextAt > s.GameTime);
    }

    [Fact]
    public void SugarLump_HarvestReturnsFalseWhenNotReady()
    {
        var s = new GameState();
        Assert.False(s.HarvestSugarLump());
        Assert.Equal(0, s.SugarLumps);
    }

    [Fact]
    public void BuildingLevels_BoostOnlyThatBuilding()
    {
        var s = new GameState { Cookies = 1_000_000 };
        s.BuyBuilding(BuildingId.Grandma);
        s.BuyBuilding(BuildingId.Farm);
        var grandmaBefore = s.GetBuildingUnitCps(BuildingId.Grandma);
        var farmBefore = s.GetBuildingUnitCps(BuildingId.Farm);

        // Invest 10 levels into Grandma via reflection on the balance + method.
        typeof(GameState).GetProperty("SugarLumps")!.SetValue(s, 1000L);
        for (var i = 0; i < 10; i++)
            Assert.True(s.LevelUpBuilding(BuildingId.Grandma));

        Assert.Equal(10, s.BuildingLevels[BuildingId.Grandma]);
        // Grandma gains exactly +10% (additive, per ADR 0006), Farm is untouched.
        Assert.Equal(grandmaBefore * 1.10, s.GetBuildingUnitCps(BuildingId.Grandma), 6);
        Assert.Equal(farmBefore, s.GetBuildingUnitCps(BuildingId.Farm), 6);
    }

    [Fact]
    public void LevelUpBuilding_TriangularCostAndBalanceGuard()
    {
        var s = new GameState();
        typeof(GameState).GetProperty("SugarLumps")!.SetValue(s, 3L);

        // Reaching level N costs N lumps: 1 + 2 = 3 lumps buys two levels, then broke.
        Assert.Equal(1, s.BuildingLevelUpCost(BuildingId.Cursor));
        Assert.True(s.LevelUpBuilding(BuildingId.Cursor));   // spent 1, balance 2
        Assert.Equal(2, s.BuildingLevelUpCost(BuildingId.Cursor));
        Assert.True(s.LevelUpBuilding(BuildingId.Cursor));   // spent 2, balance 0
        Assert.Equal(0, s.SugarLumps);
        Assert.Equal(3, s.BuildingLevelUpCost(BuildingId.Cursor));
        Assert.False(s.LevelUpBuilding(BuildingId.Cursor));  // can't afford
        Assert.Equal(2, s.BuildingLevels[BuildingId.Cursor]);
    }

    [Fact]
    public void SugarLumps_NoLongerBoostGlobalCps()
    {
        var s = new GameState { Cookies = 10_000 };
        s.BuyBuilding(BuildingId.Grandma);
        var baseCps = s.CurrentCpsRaw();

        // A balance of unspent lumps does nothing until invested in a building.
        typeof(GameState).GetProperty("SugarLumps")!.SetValue(s, 5L);
        Assert.Equal(baseCps, s.CurrentCpsRaw(), 6);
    }

    [Fact]
    public void SugarLumpsUnlocked_TracksThresholdBalanceAndLevels()
    {
        var s = new GameState();
        Assert.False(s.SugarLumpsUnlocked);

        // Crossing the baked threshold reveals it.
        typeof(GameState).GetProperty("AllTimeCookiesBaked")!
            .SetValue(s, ProgressionConfig.SugarLumpUnlockThreshold + 1);
        Assert.True(s.SugarLumpsUnlocked);

        // A balance alone (e.g. a migrated save below the threshold) also reveals it.
        var s2 = new GameState();
        typeof(GameState).GetProperty("SugarLumps")!.SetValue(s2, 3L);
        Assert.True(s2.SugarLumpsUnlocked);
    }

    // ------------------------------------------------------------------
    // Prestige / ascension
    // ------------------------------------------------------------------

    [Fact]
    public void PrestigeAvailable_ZeroBelowThreshold()
    {
        var s = new GameState { Cookies = 0 };
        Assert.Equal(0, s.PrestigeAvailableFromAscend());
    }

    [Fact]
    public void PrestigeAvailable_ScalesWithCbrtOfBaked()
    {
        // Bake 8 * cube unit → sqrt-ish gives 2 levels.
        var s = new GameState();
        typeof(GameState).GetProperty("TotalCookiesBaked")!
            .SetValue(s, 8 * ProgressionConfig.PrestigeCubeUnit);
        Assert.Equal(2, s.PrestigeAvailableFromAscend());

        typeof(GameState).GetProperty("TotalCookiesBaked")!
            .SetValue(s, 27 * ProgressionConfig.PrestigeCubeUnit);
        Assert.Equal(3, s.PrestigeAvailableFromAscend());
    }

    [Fact]
    public void Ascend_ResetsRunKeepsMetaAndPrestige()
    {
        var s = new GameState { Cookies = 1e15 };
        s.BuyBuildingBulk(BuildingId.Cursor, 5);
        s.BuyBuildingBulk(BuildingId.Grandma, 5);
        s.Cookies = 1e15;
        s.BuyUpgrade("tier_Cursor_1");
        s.Click();

        // Force TotalCookiesBaked to cross the ascension threshold.
        typeof(GameState).GetProperty("TotalCookiesBaked")!
            .SetValue(s, 27 * ProgressionConfig.PrestigeCubeUnit);
        var expectedLevels = 3;

        Assert.True(s.Ascend());
        Assert.Equal(expectedLevels, s.PrestigeLevel);
        Assert.Equal(1, s.AscensionCount);
        Assert.Equal(0, s.Cookies);
        Assert.Equal(0, s.TotalCookiesBaked);
        Assert.Empty(s.BuildingCounts);
        Assert.Empty(s.PurchasedUpgrades);
        Assert.Equal(0, s.HandmadeClicks);
        // AllTimeCookiesBaked survives the ascension.
        Assert.True(s.AllTimeCookiesBaked > 0);
    }

    [Fact]
    public void Ascend_ReturnsFalseBelowThreshold()
    {
        var s = new GameState { Cookies = 5 };
        Assert.False(s.Ascend());
        Assert.Equal(0, s.PrestigeLevel);
    }

    [Fact]
    public void PrestigeLevels_BoostCps()
    {
        var s = new GameState { Cookies = 10_000 };
        s.BuyBuilding(BuildingId.Grandma);
        var baseCps = s.CurrentCpsRaw();

        typeof(GameState).GetProperty("PrestigeLevel")!.SetValue(s, 10);
        var boostedCps = s.CurrentCpsRaw();

        var expected = baseCps * (1 + 10 * ProgressionConfig.PrestigeCpsBonus);
        Assert.Equal(expected, boostedCps, 6);
    }

    // ------------------------------------------------------------------
    // Offline earnings
    // ------------------------------------------------------------------

    [Fact]
    public void OfflineProgress_UnderReportThreshold_ReturnsZero()
    {
        var s = new GameState { Cookies = 100 };
        s.BuyBuilding(BuildingId.Grandma);

        var summary = s.ApplyOfflineProgress(5); // below OfflineMinReportSeconds
        Assert.Equal(0, summary.CookiesEarned);
    }

    [Fact]
    public void OfflineProgress_CreditsCookiesAtReducedEfficiency()
    {
        var s = new GameState { Cookies = 10_000 };
        s.BuyBuildingBulk(BuildingId.Grandma, 10); // 10 CPS
        var cps = s.CurrentCpsRaw();
        Assert.Equal(10, cps, 6);

        var before = s.Cookies;
        var summary = s.ApplyOfflineProgress(60); // 1 minute
        var expected = cps * 60 * ProgressionConfig.OfflineEfficiency;
        Assert.Equal(expected, summary.CookiesEarned, 4);
        Assert.Equal(before + expected, s.Cookies, 4);
    }

    [Fact]
    public void OfflineProgress_CappedAtMaxWindow()
    {
        var s = new GameState { Cookies = 10_000 };
        s.BuyBuildingBulk(BuildingId.Grandma, 10);

        var enormous = ProgressionConfig.OfflineMaxSeconds * 10;
        var summary = s.ApplyOfflineProgress(enormous);
        Assert.Equal(ProgressionConfig.OfflineMaxSeconds, summary.Seconds);
    }

    [Fact]
    public void OfflineProgress_AdvancesSugarLumpTimer()
    {
        var s = new GameState();
        typeof(GameState).GetProperty("AllTimeCookiesBaked")!
            .SetValue(s, ProgressionConfig.SugarLumpUnlockThreshold + 10);
        // Away for the full ripen window.
        var summary = s.ApplyOfflineProgress(ProgressionConfig.SugarLumpRipenSeconds + 60);
        Assert.True(s.SugarLumpReady);
        Assert.True(summary.SugarLumpHarvestReady);
    }

    // ------------------------------------------------------------------
    // Catch-up progress (tab backgrounded, not closed — full efficiency)
    // ------------------------------------------------------------------

    [Fact]
    public void CatchUp_CreditsCookiesAtFullEfficiency()
    {
        var s = new GameState { Cookies = 10_000 };
        s.BuyBuildingBulk(BuildingId.Grandma, 10); // 10 CPS
        var cps = s.CurrentCpsRaw();
        Assert.Equal(10, cps, 6);

        var before = s.Cookies;
        s.CatchUpProgress(60); // 1 minute backgrounded
        // No 50% haircut, unlike ApplyOfflineProgress.
        var expected = cps * 60;
        Assert.Equal(before + expected, s.Cookies, 4);
    }

    [Fact]
    public void CatchUp_AdvancesGameClock()
    {
        var s = new GameState { Cookies = 10_000 };
        s.BuyBuildingBulk(BuildingId.Grandma, 5);
        var before = s.GameTime;

        s.CatchUpProgress(120);

        Assert.Equal(before + 120, s.GameTime, 6);
    }

    [Fact]
    public void CatchUp_DoesNotStretchExpiringFrenzyAcrossWindow()
    {
        var s = new GameState { Cookies = 10_000 };
        s.BuyBuildingBulk(BuildingId.Grandma, 10); // 10 raw CPS
        var raw = s.CurrentCpsRaw();

        // A x7 Frenzy that expires almost immediately must not inflate a long
        // catch-up window: catch-up credits raw CPS, and the buff is expired
        // once the clock advances past it.
        s.Buffs.Add(new ActiveBuff(GoldenCookieEffect.Frenzy, 7.0, s.GameTime + 1));

        var before = s.Cookies;
        s.CatchUpProgress(600); // 10 minutes

        Assert.Equal(before + raw * 600, s.Cookies, 4);
        Assert.Empty(s.Buffs); // the frenzy expired, not carried forward
    }

    [Fact]
    public void CatchUp_NonPositiveIsNoOp()
    {
        var s = new GameState { Cookies = 500 };
        s.BuyBuildingBulk(BuildingId.Grandma, 3);
        var cookies = s.Cookies;
        var time = s.GameTime;

        s.CatchUpProgress(0);
        s.CatchUpProgress(-10);

        Assert.Equal(cookies, s.Cookies);
        Assert.Equal(time, s.GameTime);
    }

    // ------------------------------------------------------------------
    // News queue
    // ------------------------------------------------------------------

    [Fact]
    public void News_ClickingGoldenCookie_EnqueuesFlavor()
    {
        // Use a fixed random so we know the spawn happens deterministically.
        var s = new GameState(new Random(1234));
        // Fast-forward until a golden appears.
        for (int i = 0; i < 500 && s.ActiveGolden is null; i++) s.Tick(1);
        Assert.NotNull(s.ActiveGolden);
        s.DrainNewsMessages(); // discard spawn message

        var effect = s.ClickGoldenCookie();
        Assert.NotNull(effect);
        var msgs = s.DrainNewsMessages();
        Assert.NotEmpty(msgs);
    }

    [Fact]
    public void News_AchievementUnlock_EnqueuesFlavor()
    {
        var s = new GameState();
        s.Click();
        s.Tick(0.1); // achievement predicates are evaluated inside Tick
        var msgs = s.DrainNewsMessages();
        // News lines are now keys + args; resolve them through the English
        // localizer and assert the "First bite" unlock line came through.
        var loc = new Localizer(Language.English);
        Assert.Contains(msgs, m => NewsFlavor.Resolve(m, loc).Contains("First bite"));
    }
}
