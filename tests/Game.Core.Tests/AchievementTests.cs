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
