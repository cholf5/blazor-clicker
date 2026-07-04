using Game.Core.Domain;

namespace Game.Core.Tests;

public class AchievementTests
{
    [Fact]
    public void BakingCookies_UnlocksBakedAchievement()
    {
        var s = new GameState();
        // Click 100 times to earn "Wake and bake" (>=1 cookie).
        s.Click();
        s.Tick(0.001); // trigger achievement check
        Assert.Contains("baked_1", s.UnlockedAchievements);
    }

    [Fact]
    public void OwningCursor_UnlocksOwnAchievement()
    {
        var s = new GameState { Cookies = 100 };
        s.BuyBuilding(BuildingId.Cursor);
        s.Tick(0.001);
        Assert.Contains("own_Cursor_1", s.UnlockedAchievements);
    }

    [Fact]
    public void Achievements_QueueNotifications_OnlyOnce()
    {
        var s = new GameState();
        s.Click();
        s.Tick(0.001);

        var first = s.DrainAchievementNotifications();
        Assert.NotEmpty(first);

        // Second drain should be empty because we already flushed.
        var second = s.DrainAchievementNotifications();
        Assert.Empty(second);

        // Second tick shouldn't re-queue already-unlocked ones.
        s.Tick(0.001);
        Assert.Empty(s.DrainAchievementNotifications());
    }

    [Fact]
    public void ClickMilestone_UnlocksClickAchievement()
    {
        var s = new GameState();
        for (var i = 0; i < 100; i++) s.Click();
        s.Tick(0.001);
        Assert.Contains("clicks_100", s.UnlockedAchievements);
    }

    [Fact]
    public void OwningHighBuildingCount_UnlocksTopTierOwnAchievement()
    {
        // 600× ownership is the highest building tier. Seed enough cookies to
        // afford 600 cursors, then buy them.
        var s = new GameState { Cookies = double.MaxValue };
        for (var i = 0; i < 600; i++) s.BuyBuilding(BuildingId.Cursor);
        s.Tick(0.001);

        Assert.Equal(600, s.BuildingCounts[BuildingId.Cursor]);
        Assert.Contains("own_Cursor_600", s.UnlockedAchievements);
        Assert.Contains("own_Cursor_550", s.UnlockedAchievements);
    }

    [Fact]
    public void HighBakeMilestone_UnlocksTrillionTier()
    {
        // TotalCookiesBaked has a private setter, so seed it via a save load.
        var s = LoadWithTotalBaked(1_000_000_000_000); // 1e12
        s.Tick(0.001);
        Assert.Contains("baked_1000000000000", s.UnlockedAchievements);
    }

    [Fact]
    public void BakeMilestone_BelowThreshold_StaysLocked()
    {
        var s = LoadWithTotalBaked(999_999_999_999); // just under 1e12
        s.Tick(0.001);
        Assert.DoesNotContain("baked_1000000000000", s.UnlockedAchievements);
    }

    [Fact]
    public void ReachingCps_UnlocksCpsMilestone()
    {
        // One grandma produces >= 1 cps, enough for the first cps tier.
        var s = new GameState { Cookies = double.MaxValue };
        s.BuyBuilding(BuildingId.Grandma);
        Assert.True(s.CurrentCps() >= 1);

        s.Tick(0.001);
        Assert.Contains("cps_1", s.UnlockedAchievements);
    }

    [Fact]
    public void CpsMilestone_StaysLocked_WhenIdle()
    {
        var s = new GameState();
        s.Tick(0.001);
        Assert.DoesNotContain("cps_1", s.UnlockedAchievements);
    }

    [Fact]
    public void OwningEveryUpgrade_UnlocksCompleteArsenal()
    {
        var s = new GameState { Cookies = double.MaxValue };
        // Buying is gated by unlock predicates, so seed the full set directly
        // through a save load instead of trying to satisfy every prerequisite.
        foreach (var up in Game.Core.Data.Upgrades.All)
            s.PurchasedUpgrades.Add(up.Id);

        s.Tick(0.001);
        Assert.Equal(83, s.PurchasedUpgrades.Count);
        Assert.Contains("upgrades_83", s.UnlockedAchievements);
    }

    [Fact]
    public void HandmadeClicks_AccumulateHandmadeCookies_AndUnlock()
    {
        // Each click yields at least 1 cookie; 1000 clicks clears handmade_1000.
        var s = new GameState();
        for (var i = 0; i < 1_000; i++) s.Click();
        s.Tick(0.001);

        Assert.True(s.HandmadeCookies >= 1_000);
        Assert.Contains("handmade_1000", s.UnlockedAchievements);
    }

    [Fact]
    public void PassiveCps_DoesNotCountAsHandmade()
    {
        var s = new GameState { Cookies = double.MaxValue };
        s.BuyBuildingBulk(BuildingId.Grandma, 10);
        s.Tick(100); // large passive yield, zero clicks

        Assert.Equal(0, s.HandmadeCookies);
    }

    [Fact]
    public void ClickingGoldenDuringFrenzy_CountsAsCombo_AndUnlocks()
    {
        // Find a seed that produces a Frenzy, click it (starts the 77s buff),
        // then catch a second golden cookie that spawns before the buff ends.
        for (var seed = 0; seed < 500; seed++)
        {
            var s = new GameState(new Random(seed));
            s.BuyBuildingBulk(BuildingId.Cursor, 100);
            s.Cookies = 1_000_000;
            s.Tick(100);
            if (s.ActiveGolden?.Effect != GoldenCookieEffect.Frenzy) continue;

            s.ClickGoldenCookie(); // frenzy now active, not itself a combo
            Assert.Equal(0, s.GoldenClicksDuringFrenzy);

            // A second golden is scheduled 60-300s out; only those landing
            // within the 77s frenzy window qualify. Tick to 76s to catch one.
            s.Tick(76);
            if (s.ActiveGolden is null) continue;

            s.ClickGoldenCookie(); // clicked during active frenzy → combo
            s.Tick(0.001);
            Assert.Equal(1, s.GoldenClicksDuringFrenzy);
            Assert.Contains("combo_1", s.UnlockedAchievements);
            return;
        }
        Assert.Fail("No frenzy+second-golden sequence appeared in 500 seeds.");
    }

    [Fact]
    public void Playtime_UnlocksAfterOneHour()
    {
        var s = new GameState();
        s.Tick(3_600);
        Assert.Contains("playtime_3600", s.UnlockedAchievements);
    }

    // Builds a GameState with a specific lifetime-baked total by round-tripping
    // through the save system (TotalCookiesBaked is not directly settable).
    private static GameState LoadWithTotalBaked(double total)
    {
        var json = $$"""
        {
          "Version": 1,
          "Cookies": {{total:0}},
          "TotalCookiesBaked": {{total:0}},
          "HandmadeClicks": 0,
          "GoldenCookiesClicked": 0,
          "GameTime": 0,
          "BuildingCounts": {},
          "PurchasedUpgrades": [],
          "UnlockedAchievements": [],
          "Buffs": [],
          "ActiveGolden": null,
          "NextGoldenAt": 100
        }
        """;
        return SaveSystem.DeserializeFromJson(json);
    }
}
