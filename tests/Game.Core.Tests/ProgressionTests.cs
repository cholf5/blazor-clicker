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
    public void SugarLumps_BoostCps()
    {
        var s = new GameState { Cookies = 10_000 };
        s.BuyBuilding(BuildingId.Grandma);
        var baseCps = s.CurrentCpsRaw();

        // Grant 5 sugar lumps via reflection since the intended path is player harvest.
        typeof(GameState).GetProperty("SugarLumps")!.SetValue(s, 5L);
        var boostedCps = s.CurrentCpsRaw();

        var expected = baseCps * (1 + 5 * ProgressionConfig.SugarLumpCpsBonus);
        Assert.Equal(expected, boostedCps, 6);
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
