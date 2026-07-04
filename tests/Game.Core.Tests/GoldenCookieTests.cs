using Game.Core.Domain;

namespace Game.Core.Tests;

public class GoldenCookieTests
{
    /// <summary>Deterministic RNG so scheduling behaves predictably in tests.</summary>
    private static GameState NewSeeded(int seed) => new(new Random(seed));

    [Fact]
    public void GoldenCookie_SpawnsAfterInitialDelay()
    {
        var s = NewSeeded(42);
        // Initial delay is 60-90 seconds; a big tick guarantees spawn.
        s.Tick(100);
        Assert.NotNull(s.ActiveGolden);
    }

    [Fact]
    public void GoldenCookie_ClickingLuckyGivesCookies()
    {
        var s = NewSeeded(1);
        s.Cookies = 10_000;
        // Force-spawn a Lucky cookie by manipulating state via a tick until one appears.
        // Because the effect is random, retry with different seeds until we hit Lucky.
        var found = false;
        for (var seed = 0; seed < 50 && !found; seed++)
        {
            s = NewSeeded(seed);
            s.Cookies = 10_000;
            s.Tick(100);
            if (s.ActiveGolden?.Effect == GoldenCookieEffect.Lucky)
            {
                var before = s.Cookies;
                var effect = s.ClickGoldenCookie();
                Assert.Equal(GoldenCookieEffect.Lucky, effect);
                Assert.True(s.Cookies > before);
                Assert.Null(s.ActiveGolden);
                Assert.Equal(1, s.GoldenCookiesClicked);
                found = true;
            }
        }
        Assert.True(found, "50 seeds should have produced at least one Lucky cookie.");
    }

    [Fact]
    public void GoldenCookie_FrenzyAddsBuffMultiplyingCps()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var s = NewSeeded(seed);
            s.BuyBuildingBulk(BuildingId.Cursor, 100);
            s.Cookies = 1_000_000;
            s.Tick(100);
            if (s.ActiveGolden?.Effect == GoldenCookieEffect.Frenzy)
            {
                var before = s.CurrentCps();
                s.ClickGoldenCookie();
                var after = s.CurrentCps();
                Assert.Equal(before * 7, after, 4);
                return;
            }
        }
        Assert.Fail("No Frenzy golden cookie appeared in 50 seeds.");
    }

    [Fact]
    public void GoldenCookie_ExpiresAfterTimeout()
    {
        var s = NewSeeded(42);
        s.Tick(100);
        Assert.NotNull(s.ActiveGolden);
        // Default lifetime is 13s.
        s.Tick(14);
        Assert.Null(s.ActiveGolden);
    }

    [Fact]
    public void Frenzy_Buff_ExpiresAfter77Seconds()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var s = NewSeeded(seed);
            s.BuyBuildingBulk(BuildingId.Cursor, 10);
            s.Cookies = 1_000_000;
            s.Tick(100);
            if (s.ActiveGolden?.Effect == GoldenCookieEffect.Frenzy)
            {
                s.ClickGoldenCookie();
                Assert.Single(s.Buffs);
                s.Tick(78);
                Assert.Empty(s.Buffs);
                return;
            }
        }
        Assert.Fail("No Frenzy golden cookie appeared in 50 seeds.");
    }

    [Fact]
    public void ClickGoldenCookie_WhenNoneActive_ReturnsNull()
    {
        var s = NewSeeded(1);
        Assert.Null(s.ClickGoldenCookie());
    }
}
