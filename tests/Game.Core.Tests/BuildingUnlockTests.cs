using Game.Core;
using Game.Core.Data;
using Game.Core.Domain;

namespace Game.Core.Tests;

/// <summary>
/// Buildings reveal progressively: the next one appears only once the previous
/// building is owned and this run's baked total crosses a fraction of its cost.
/// These are pure-domain rules (no browser), computed on demand from state.
/// </summary>
public class BuildingUnlockTests
{
    // Grandma is the second building; unlock threshold comes from the domain's
    // single source of truth so this test can never disagree with the real gate.
    private static double GrandmaThreshold =>
        new GameState().BuildingUnlockThreshold(BuildingId.Grandma);

    [Fact]
    public void FreshState_OnlyCursorUnlocked()
    {
        var s = new GameState();

        Assert.True(s.IsBuildingUnlocked(BuildingId.Cursor));
        Assert.False(s.IsBuildingUnlocked(BuildingId.Grandma));
        Assert.False(s.IsBuildingUnlocked(BuildingId.Farm));
        Assert.Equal(BuildingId.Grandma, s.NextLockedBuilding());
    }

    [Fact]
    public void PrerequisiteMissing_StaysLocked_EvenPastThreshold()
    {
        // Baked well past Farm's threshold, but Grandma (its prerequisite) is
        // never owned, so Farm cannot unlock.
        var s = new GameState { Cookies = 100 };
        var farmThreshold = s.BuildingUnlockThreshold(BuildingId.Farm);
        while (s.TotalCookiesBaked < farmThreshold * 2) s.Click();

        Assert.False(s.IsBuildingUnlocked(BuildingId.Farm));
    }

    [Fact]
    public void ThresholdNotReached_StaysLocked_EvenWithPrerequisite()
    {
        // Own a cursor but bake far less than Grandma's threshold.
        var s = new GameState { Cookies = 100 };
        Assert.True(s.BuyBuilding(BuildingId.Cursor));

        Assert.True(s.TotalCookiesBaked < GrandmaThreshold);
        Assert.False(s.IsBuildingUnlocked(BuildingId.Grandma));
    }

    [Fact]
    public void BothConditionsMet_Unlocks_AndNextAdvances()
    {
        var s = new GameState { Cookies = 100 };
        Assert.True(s.BuyBuilding(BuildingId.Cursor));
        // Bake past Grandma's threshold via clicks.
        while (s.TotalCookiesBaked < GrandmaThreshold) s.Click();

        Assert.True(s.IsBuildingUnlocked(BuildingId.Grandma));
        Assert.Equal(BuildingId.Farm, s.NextLockedBuilding());
    }

    [Fact]
    public void UnlockIsPresentationOnly_NotEnforcedByBuyBuilding()
    {
        // Unlock is a display signal, not an economic gate: a rich player can
        // still buy a not-yet-revealed building through the domain API. The shop
        // UI is what hides it; BuyBuilding itself stays unguarded.
        var s = new GameState { Cookies = 1_000_000 };

        Assert.False(s.IsBuildingUnlocked(BuildingId.Grandma));
        Assert.True(s.BuyBuilding(BuildingId.Grandma));
        Assert.Equal(1, s.BuildingCounts[BuildingId.Grandma]);
    }

    [Fact]
    public void UnlockIsIrreversible_WhenBalanceSpentDown()
    {
        var s = new GameState { Cookies = 100 };
        s.BuyBuilding(BuildingId.Cursor);
        while (s.TotalCookiesBaked < GrandmaThreshold) s.Click();
        Assert.True(s.IsBuildingUnlocked(BuildingId.Grandma));

        // Spend the balance to zero — TotalCookiesBaked is unaffected.
        s.Cookies = 0;

        Assert.True(s.IsBuildingUnlocked(BuildingId.Grandma));
    }

    [Fact]
    public void MidGameSave_KeepsOwnedBuildingsUnlocked()
    {
        // A v3 save with mid-game buildings owned. Owning a building implies its
        // threshold was long crossed, so it must load back as unlocked.
        var data = new SaveData
        {
            Version = 3,
            TotalCookiesBaked = 1_000_000,
            BuildingCounts = new Dictionary<BuildingId, int>
            {
                [BuildingId.Cursor] = 20,
                [BuildingId.Grandma] = 10,
                [BuildingId.Farm] = 3,
            },
        };

        var s = new GameState();
        s.ApplyLoaded(data);

        Assert.True(s.IsBuildingUnlocked(BuildingId.Cursor));
        Assert.True(s.IsBuildingUnlocked(BuildingId.Grandma));
        Assert.True(s.IsBuildingUnlocked(BuildingId.Farm));
    }
}
