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
}
