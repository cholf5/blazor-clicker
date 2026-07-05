using Game.Web.Components;

namespace Game.Web.Tests;

/// <summary>
/// Guards the pure layout math behind the decorative cursor ring. The invariant
/// that matters most: no matter how large the owned cursor count grows, the
/// number of rendered fingers never exceeds <see cref="CursorRingLayout.MaxFingers"/>
/// (160) — that's the whole reason the ring uses colour-tier merging instead of
/// unbounded DOM elements. The rest verifies the merge thresholds and edges.
/// </summary>
public class CursorRingLayoutTests
{
    [Fact]
    public void ZeroCursors_RendersNoFingers()
    {
        var model = CursorRingLayout.Compute(0);
        Assert.Empty(model.Fingers);
        Assert.Equal(0, model.Tier);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(160)]
    public void UpToFullRings_DrawsOneWhiteFingerPerCursor(long count)
    {
        var model = CursorRingLayout.Compute(count);
        Assert.Equal((int)count, model.Fingers.Count);
        Assert.Equal(0, model.Tier);       // white
        Assert.Equal(1, model.PerFinger);
        Assert.All(model.Fingers, f => Assert.Equal(0, f.Tier));
    }

    [Fact]
    public void JustPastFull_MergesToGreen()
    {
        // 161 no longer fits in 160 white slots, so tier climbs to 1 (green):
        // each finger now represents 2 cursors → ceil(161/2) = 81 fingers.
        var model = CursorRingLayout.Compute(161);
        Assert.Equal(1, model.Tier);
        Assert.Equal(2, model.PerFinger);
        Assert.Equal(81, model.Fingers.Count);
        Assert.All(model.Fingers, f => Assert.Equal(1, f.Tier));
    }

    [Fact]
    public void GreenSaturates_ThenMergesToBlue()
    {
        // Green holds up to 160*2 = 320 cursors. 320 is still green (160 fingers);
        // 321 pushes to blue (tier 2, ×4).
        Assert.Equal(1, CursorRingLayout.Compute(320).Tier);
        Assert.Equal(160, CursorRingLayout.Compute(320).Fingers.Count);

        var blue = CursorRingLayout.Compute(321);
        Assert.Equal(2, blue.Tier);
        Assert.Equal(4, blue.PerFinger);
    }

    [Fact]
    public void NextMergeAt_ReportsTheThreshold()
    {
        // White merges when count first exceeds 160.
        Assert.Equal(161, CursorRingLayout.Compute(100).NextMergeAt);
        // Green merges when count first exceeds 320.
        Assert.Equal(321, CursorRingLayout.Compute(200).NextMergeAt);
    }

    [Fact]
    public void AstronomicalCount_ClampsToGoldCeiling()
    {
        // Far past what even gold (tier 6, ×64 → holds 160*64 = 10,240) can
        // represent one-finger-per-slot. The ring must clamp: 160 gold fingers,
        // no further growth, no null-free merge target.
        var model = CursorRingLayout.Compute(long.MaxValue);
        Assert.Equal(CursorRingLayout.MaxTier, model.Tier);            // gold
        Assert.Equal(CursorRingLayout.MaxFingers, model.Fingers.Count); // 160
        Assert.Null(model.NextMergeAt);                                 // true ceiling
        Assert.All(model.Fingers, f => Assert.Equal(6, f.Tier));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(160)]
    [InlineData(161)]
    [InlineData(10_000)]
    [InlineData(1_000_000_000_000)]
    public void FingerCount_NeverExceedsBudget(long count)
    {
        var model = CursorRingLayout.Compute(count);
        Assert.True(model.Fingers.Count <= CursorRingLayout.MaxFingers,
            $"count={count} produced {model.Fingers.Count} fingers, over budget");
    }

    [Fact]
    public void Fingers_FillInnermostRingFirst_WithStableIndices()
    {
        // 22 cursors → ring 0 (cap 18) full, 4 spill into ring 1.
        var model = CursorRingLayout.Compute(22);
        Assert.Equal(18, model.Fingers.Count(f => f.Ring == 0));
        Assert.Equal(4, model.Fingers.Count(f => f.Ring == 1));
        // Global indices are a stable 0..n-1 sequence (used as render keys).
        Assert.Equal(Enumerable.Range(0, 22), model.Fingers.Select(f => f.GlobalIndex));
    }
}
