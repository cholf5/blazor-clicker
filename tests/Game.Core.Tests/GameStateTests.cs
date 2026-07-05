using Game.Core.Domain;
using Game.Core.Data;

namespace Game.Core.Tests;

public class GameStateTests
{
    [Fact]
    public void NewGame_StartsEmpty()
    {
        var s = new GameState();
        Assert.Equal(0, s.Cookies);
        Assert.Equal(0, s.TotalCookiesBaked);
        Assert.Empty(s.BuildingCounts);
        Assert.Empty(s.PurchasedUpgrades);
    }

    [Fact]
    public void Click_IncreasesCookiesAndBaked()
    {
        var s = new GameState();
        var gained = s.Click();
        Assert.Equal(1, gained);
        Assert.Equal(1, s.Cookies);
        Assert.Equal(1, s.TotalCookiesBaked);
        Assert.Equal(1, s.HandmadeClicks);
    }

    [Fact]
    public void Tick_WithNoBuildings_ProducesNothing()
    {
        var s = new GameState();
        s.Tick(1.0);
        Assert.Equal(0, s.Cookies);
    }

    [Fact]
    public void Tick_WithGrandma_ProducesCps()
    {
        var s = new GameState { Cookies = 10_000 };
        s.BuyBuilding(BuildingId.Grandma); // costs 100, gives 1 CPS
        Assert.Equal(1, s.BuildingCounts[BuildingId.Grandma]);
        Assert.Equal(1, s.CurrentCps(), 6);

        var before = s.Cookies;
        s.Tick(2.0);
        Assert.Equal(before + 2, s.Cookies, 4);
        Assert.True(s.TotalCookiesBaked > 0);
    }

    [Fact]
    public void Tick_NegativeOrZeroDelta_IsNoop()
    {
        var s = new GameState { Cookies = 100 };
        s.BuyBuilding(BuildingId.Grandma);
        var before = s.Cookies;
        s.Tick(0);
        s.Tick(-1);
        Assert.Equal(before, s.Cookies);
    }

    [Fact]
    public void BuildingCpsShare_SumsToOne()
    {
        var s = new GameState { Cookies = 1_000_000 };
        s.BuyBuildingBulk(BuildingId.Cursor, 10);
        s.BuyBuildingBulk(BuildingId.Grandma, 5);

        var total = s.BuildingCpsShare(BuildingId.Cursor) + s.BuildingCpsShare(BuildingId.Grandma);
        Assert.Equal(1.0, total, 6);
    }

    [Fact]
    public void BuildingCpsShare_UnaffectedByFrenzyBuff()
    {
        var s = new GameState { Cookies = 1_000_000 };
        s.BuyBuildingBulk(BuildingId.Cursor, 10);
        s.BuyBuildingBulk(BuildingId.Grandma, 5);

        var before = s.BuildingCpsShare(BuildingId.Grandma);

        // A x7 Frenzy scales every building uniformly, so shares must not move.
        s.Buffs.Add(new ActiveBuff(GoldenCookieEffect.Frenzy, 7.0, s.GameTime + 77));
        Assert.Equal(before, s.BuildingCpsShare(BuildingId.Grandma), 6);
    }

    [Fact]
    public void BuildingCpsShare_ZeroWhenNothingProduces()
    {
        var s = new GameState();
        Assert.Equal(0, s.BuildingCpsShare(BuildingId.Grandma));
    }
}
