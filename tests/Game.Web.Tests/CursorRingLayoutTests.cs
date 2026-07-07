using Game.Web.Components;

namespace Game.Web.Tests;

/// <summary>
/// Guards the pure layout math behind the decorative cursor ring. The invariant
/// that matters most: no matter how large the owned cursor count grows, the
/// number of rendered fingers never exceeds <see cref="CursorRingLayout.MaxFingers"/>
/// — that's the whole reason the ring uses colour-tier merging instead of
/// unbounded DOM elements. The rest verifies the merge thresholds and edges.
///
/// <para>These tests derive their expectations from
/// <see cref="CursorRingLayout.RingCapacities"/> (the single source of truth) and
/// <see cref="CursorRingLayout.MaxFingers"/> rather than hard-coding 160 — so
/// re-tuning the ring shape in one place keeps the suite meaningful instead of
/// forcing a parallel edit here.</para>
/// </summary>
public class CursorRingLayoutTests
{
    // Convenience mirrors of the source-of-truth budget and its first ring's cap.
    private static int Budget => CursorRingLayout.MaxFingers;
    private static int FirstRing => CursorRingLayout.RingCapacities[0];

    [Fact]
    public void MaxFingers_IsTheSumOfRingCapacities()
    {
        // The budget must never drift from the array it's derived from.
        Assert.Equal(CursorRingLayout.RingCapacities.Sum(), CursorRingLayout.MaxFingers);
    }

    [Fact]
    public void ZeroCursors_RendersNoFingers()
    {
        var model = CursorRingLayout.Compute(0);
        Assert.Empty(model.Fingers);
        Assert.Equal(0, model.Tier);
    }

    [Theory]
    [InlineData(1)]
    public void SingleCursor_DrawsOneWhiteFinger(long count)
    {
        var model = CursorRingLayout.Compute(count);
        Assert.Equal((int)count, model.Fingers.Count);
        Assert.Equal(0, model.Tier);
        Assert.Equal(1, model.PerFinger);
    }

    [Fact]
    public void UpToFullRings_DrawsOneWhiteFingerPerCursor()
    {
        // Anything up to the full budget stays white, one finger per cursor.
        foreach (var count in new long[] { 1, Budget / 2, Budget })
        {
            var model = CursorRingLayout.Compute(count);
            Assert.Equal((int)count, model.Fingers.Count);
            Assert.Equal(0, model.Tier);       // white
            Assert.Equal(1, model.PerFinger);
            Assert.All(model.Fingers, f => Assert.Equal(0, f.Tier));
        }
    }

    [Fact]
    public void JustPastFull_MergesToGreen()
    {
        // Budget+1 no longer fits in the white slots, so tier climbs to 1 (green):
        // each finger now represents 2 cursors → ceil((Budget+1)/2) fingers.
        var count = (long)Budget + 1;
        var model = CursorRingLayout.Compute(count);
        Assert.Equal(1, model.Tier);
        Assert.Equal(2, model.PerFinger);
        Assert.Equal((int)((count + 1) / 2), model.Fingers.Count);
        Assert.All(model.Fingers, f => Assert.Equal(1, f.Tier));
    }

    [Fact]
    public void GreenSaturates_ThenMergesToBlue()
    {
        // Green holds up to Budget*2 cursors. That's still green (Budget fingers);
        // one more pushes to blue (tier 2, ×4).
        Assert.Equal(1, CursorRingLayout.Compute(Budget * 2L).Tier);
        Assert.Equal(Budget, CursorRingLayout.Compute(Budget * 2L).Fingers.Count);

        var blue = CursorRingLayout.Compute(Budget * 2L + 1);
        Assert.Equal(2, blue.Tier);
        Assert.Equal(4, blue.PerFinger);
    }

    [Fact]
    public void NextMergeAt_ReportsTheThreshold()
    {
        // White merges when count first exceeds the budget.
        Assert.Equal(Budget + 1L, CursorRingLayout.Compute(1).NextMergeAt);
        // Green merges when count first exceeds Budget*2.
        Assert.Equal(Budget * 2L + 1, CursorRingLayout.Compute(Budget + 1L).NextMergeAt);
    }

    [Fact]
    public void AstronomicalCount_ClampsToGoldCeiling()
    {
        // Far past what even gold (tier 6, ×64) can represent one-finger-per-slot.
        // The ring must clamp: MaxFingers gold fingers, no further growth, no
        // non-null merge target.
        var model = CursorRingLayout.Compute(long.MaxValue);
        Assert.Equal(CursorRingLayout.MaxTier, model.Tier);            // gold
        Assert.Equal(CursorRingLayout.MaxFingers, model.Fingers.Count);
        Assert.Null(model.NextMergeAt);                                 // true ceiling
        Assert.All(model.Fingers, f => Assert.Equal(6, f.Tier));
    }

    [Theory]
    [InlineData(1)]
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
        // Fill the first ring, then spill 4 into the second.
        var count = FirstRing + 4;
        var model = CursorRingLayout.Compute(count);
        Assert.Equal(FirstRing, model.Fingers.Count(f => f.Ring == 0));
        Assert.Equal(4, model.Fingers.Count(f => f.Ring == 1));
        // Global indices are a stable 0..n-1 sequence (used as render keys).
        Assert.Equal(Enumerable.Range(0, count), model.Fingers.Select(f => f.GlobalIndex));
    }
}
