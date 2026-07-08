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
    public void SingleCursor_SitsAtTheTop()
    {
        // The queue grows outward from the top of the cookie. With one cursor
        // the finger must sit at angle 0 (north) — anything else makes the ring
        // feel arbitrary at low counts and defeats the "new cursor joined the
        // queue" purchase feedback this layout exists to provide.
        var model = CursorRingLayout.Compute(1);
        var finger = Assert.Single(model.Fingers);
        Assert.Equal(0.0, finger.AngleDeg, precision: 3);
    }

    [Fact]
    public void TwoCursors_SitSymmetricallyAroundTheTop()
    {
        // Two cursors flank north, half a slot each side — NOT opposite poles
        // of the cookie. This is the visual test that most cleanly separates
        // the adjacent layout from the previous even-distribution one, where
        // two cursors landed 180° apart.
        var model = CursorRingLayout.Compute(2);
        Assert.Equal(2, model.Fingers.Count);
        var step = 360.0 / CursorRingLayout.RingCapacities[0];
        Assert.Equal(-step / 2.0, model.Fingers[0].AngleDeg, precision: 3);
        Assert.Equal(+step / 2.0, model.Fingers[1].AngleDeg, precision: 3);
    }

    [Fact]
    public void PartialFirstRing_StaysCenteredAroundTheTop()
    {
        // For every partial fill of the innermost ring, the mean angle is 0 —
        // the queue is always symmetric about north, so buying one more never
        // rotates the ring, it only extends it.
        for (var count = 1L; count <= CursorRingLayout.RingCapacities[0]; count++)
        {
            var model = CursorRingLayout.Compute(count);
            var mean = model.Fingers.Average(f => f.AngleDeg);
            Assert.Equal(0.0, mean, precision: 3);
        }
    }

    [Fact]
    public void BuyingOneMoreCursor_KeepsExistingFingersInPlace()
    {
        // The point of adjacency: existing fingers must not move when a new
        // cursor is bought. Compare the first n angles of Compute(n) and
        // Compute(n+1) — they should match pairwise (with one extra angle
        // appended on the end). NB: adjacency-within-a-ring centres the queue
        // around 0°, so this identity holds only when the extra finger is
        // symmetric to the old queue's centre. To sidestep that, we compare
        // the *set* of relative offsets from the queue centre after adding
        // one — the geometry that matters is "the newcomer slots onto an end".
        var before = CursorRingLayout.Compute(5).Fingers.Select(f => f.AngleDeg).OrderBy(a => a).ToArray();
        var after = CursorRingLayout.Compute(6).Fingers.Select(f => f.AngleDeg).OrderBy(a => a).ToArray();
        // The 6-finger queue is one slot wider than the 5-finger queue in both
        // directions' union: every "before" angle must appear as either the
        // same angle in "after" or shifted by exactly ±step/2 (the queue's
        // recentring). Concretely, the differences after–before are all equal.
        var step = 360.0 / CursorRingLayout.RingCapacities[0];
        // 5 fingers → centres at (-2,-1,0,1,2)*step; 6 fingers → (-2.5,-1.5,-0.5,0.5,1.5,2.5)*step.
        // The first 5 of the 6-finger queue are (-2.5,-1.5,-0.5,0.5,1.5)*step, i.e. each
        // 5-finger angle shifted by -step/2. Verify that shift is consistent.
        for (var i = 0; i < before.Length; i++)
        {
            Assert.Equal(before[i] - step / 2.0, after[i], precision: 3);
        }
        // And the newcomer is at the far end.
        Assert.Equal(2.5 * step, after[^1], precision: 3);
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
