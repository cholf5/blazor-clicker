using Game.Core.Domain;
using Game.Core.Data;

namespace Game.Core.Tests;

/// <summary>
/// Covers the domain-level debug/GM helpers. They ship in every build (so this
/// Release test run can reach them) but are only ever called from a Debug-only GM
/// panel, so the important guarantees are: they grant for free, they credit the
/// same counters normal play would, and they no-op on non-positive amounts.
/// </summary>
public class DebugCheatTests
{
    [Fact]
    public void DebugAddCookies_CreditsBankAndBakedTotals()
    {
        var s = new GameState();
        s.DebugAddCookies(1_000);
        Assert.Equal(1_000, s.Cookies, 4);
        // Baked totals move too, so milestone achievements / prestige stay honest.
        Assert.Equal(1_000, s.TotalCookiesBaked, 4);
        Assert.Equal(1_000, s.AllTimeCookiesBaked, 4);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void DebugAddCookies_NonPositive_IsNoOp(double amount)
    {
        var s = new GameState { Cookies = 42 };
        s.DebugAddCookies(amount);
        Assert.Equal(42, s.Cookies, 4);
    }

    [Fact]
    public void DebugAddBuilding_AddsForFree_WithoutSpending()
    {
        var s = new GameState { Cookies = 10 };
        s.DebugAddBuilding(BuildingId.Cursor, 200);
        Assert.Equal(200, s.BuildingCounts[BuildingId.Cursor]);
        Assert.Equal(10, s.Cookies, 4); // no cost deducted
    }

    [Fact]
    public void DebugAddBuilding_Accumulates()
    {
        var s = new GameState();
        s.DebugAddBuilding(BuildingId.Grandma, 3);
        s.DebugAddBuilding(BuildingId.Grandma, 4);
        Assert.Equal(7, s.BuildingCounts[BuildingId.Grandma]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DebugAddBuilding_NonPositive_IsNoOp(int count)
    {
        var s = new GameState();
        s.DebugAddBuilding(BuildingId.Cursor, count);
        Assert.Empty(s.BuildingCounts);
    }

    [Fact]
    public void DebugUnlockAllUpgrades_GrantsEveryCatalogUpgrade()
    {
        var s = new GameState();
        s.DebugUnlockAllUpgrades();
        Assert.Equal(Upgrades.All.Count, s.PurchasedUpgrades.Count);
        Assert.All(Upgrades.All, u => Assert.Contains(u.Id, s.PurchasedUpgrades));
    }

    [Fact]
    public void DebugAddSugarLumps_AddsToBalance()
    {
        var s = new GameState();
        s.DebugAddSugarLumps(5);
        Assert.Equal(5, s.SugarLumps);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void DebugAddSugarLumps_NonPositive_IsNoOp(long count)
    {
        var s = new GameState();
        s.DebugAddSugarLumps(count);
        Assert.Equal(0, s.SugarLumps);
    }
}
