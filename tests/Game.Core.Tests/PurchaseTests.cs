using Game.Core.Domain;
using Game.Core.Data;

namespace Game.Core.Tests;

public class PurchaseTests
{
    [Fact]
    public void BuyBuilding_InsufficientFunds_Fails()
    {
        var s = new GameState();
        Assert.False(s.BuyBuilding(BuildingId.Cursor));
        Assert.Empty(s.BuildingCounts);
    }

    [Fact]
    public void BuyBuilding_DeductsCorrectCost_Then_NextCostGrows()
    {
        var s = new GameState { Cookies = 1_000 };
        Assert.True(s.BuyBuilding(BuildingId.Cursor)); // 15
        Assert.Equal(985, s.Cookies, 4);
        var nextCost = s.NextBuildingCost(BuildingId.Cursor);
        Assert.Equal(15 * 1.15, nextCost, 4);
    }

    [Fact]
    public void BuyBuildingBulk_StopsWhenBroke()
    {
        var s = new GameState { Cookies = 50 };
        // Cursor costs 15, then 17.25, then 19.83... total ~52 for 3 → afford 2.
        var bought = s.BuyBuildingBulk(BuildingId.Cursor, 10);
        Assert.Equal(2, bought);
        Assert.Equal(2, s.BuildingCounts[BuildingId.Cursor]);
    }

    [Fact]
    public void BuyUpgrade_RequiresUnlockAndFunds()
    {
        var s = new GameState { Cookies = 1_000_000 };
        // Reinforced cursor tier1: costs 15 * 100 = 1500, requires 1 cursor
        var id = "tier_Cursor_1";
        Assert.False(s.BuyUpgrade(id)); // not unlocked yet (no cursor owned)

        s.Cookies = 1_000_000;
        s.BuyBuilding(BuildingId.Cursor);
        Assert.True(s.BuyUpgrade(id));
        Assert.Contains(id, s.PurchasedUpgrades);
    }

    [Fact]
    public void BuyUpgrade_TwiceOnSameId_Fails()
    {
        var s = new GameState { Cookies = 1_000_000 };
        s.BuyBuilding(BuildingId.Cursor);
        Assert.True(s.BuyUpgrade("tier_Cursor_1"));
        Assert.False(s.BuyUpgrade("tier_Cursor_1"));
    }

    [Fact]
    public void BuildingTierUpgrade_DoublesCps()
    {
        var s = new GameState { Cookies = 10_000_000 };
        s.BuyBuilding(BuildingId.Grandma); // 1 CPS/grandma
        var before = s.GetBuildingUnitCps(BuildingId.Grandma);
        Assert.True(s.BuyUpgrade("tier_Grandma_1"));
        var after = s.GetBuildingUnitCps(BuildingId.Grandma);
        Assert.Equal(before * 2, after, 6);
    }

    [Fact]
    public void ClickUpgrade_DoublesClickPower()
    {
        var s = new GameState { Cookies = 1_000_000 };
        // click_reinforced_finger unlocks at 50 total baked; simulate by clicking.
        for (var i = 0; i < 60; i++) s.Click();
        var before = s.ClickPower();
        Assert.True(s.BuyUpgrade("click_reinforced_finger"));
        var after = s.ClickPower();
        Assert.True(after >= before * 2 - 0.001);
    }

    [Fact]
    public void ThousandFingers_AddsCursorCpsPerNonCursorBuilding()
    {
        var s = new GameState { Cookies = 100_000_000 };
        // Need 25 cursors to unlock the synergy.
        s.BuyBuildingBulk(BuildingId.Cursor, 25);
        s.Cookies = 100_000_000;
        s.BuyBuilding(BuildingId.Grandma);
        var cursorBefore = s.GetBuildingUnitCps(BuildingId.Cursor);

        Assert.True(s.BuyUpgrade("cursor_thousand_fingers"));
        var cursorAfter = s.GetBuildingUnitCps(BuildingId.Cursor);
        Assert.True(cursorAfter > cursorBefore, $"expected {cursorAfter} > {cursorBefore}");
        // With 1 non-cursor building and 0.1 per building, added 0.1 to base 0.1 → 0.2.
        Assert.Equal(0.1 + 0.1, cursorAfter, 4);
    }

    [Fact]
    public void GlobalCpsMultiplier_ScalesTotalCps()
    {
        var s = new GameState { Cookies = 100_000_000_000 };
        s.BuyBuilding(BuildingId.Grandma); // 1 CPS
        // Bake 1M so global_kitten_helpers unlocks.
        s.Cookies = 100_000_000;
        var tCookies = s.TotalCookiesBaked;
        // Force TotalCookiesBaked past 1M via internal path — via clicks would take forever.
        // Use a big grandma tick.
        s.BuyBuildingBulk(BuildingId.Grandma, 100); // more grandmas
        s.Cookies = 100_000_000;
        while (s.TotalCookiesBaked < 1_000_000) s.Tick(100);
        s.Cookies = 100_000_000_000;

        var before = s.CurrentCps();
        Assert.True(s.BuyUpgrade("global_kitten_helpers"));
        var after = s.CurrentCps();
        Assert.Equal(before * 1.1, after, 4);
    }
}
