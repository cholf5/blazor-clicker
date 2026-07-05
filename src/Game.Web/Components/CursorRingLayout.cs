using System;
using System.Collections.Generic;

namespace Game.Web.Components;

/// <summary>
/// Pure, testable layout math for the decorative cursor ring drawn behind the
/// big cookie. Given the number of owned Cursor buildings, it produces the list
/// of fingers to render — their ring, angle, radius and colour tier.
///
/// <para><b>Why a fixed on-screen budget.</b> Each finger is a live-animated DOM
/// element; unlike the original game's single &lt;canvas&gt;, DOM layout/paint
/// cost grows with element count regardless of how fast WASM runs. So the ring
/// holds at most 5 concentric rings — 16/24/32/40/48 = <see cref="MaxFingers"/>
/// slots. Growth beyond that is expressed by <i>colour</i>, not more elements:
/// once the rings are full we pick the lowest colour tier T such that
/// ceil(count / 2^T) still fits in 160 slots, and draw that many fingers all at
/// tier T. Each tier up means every finger "represents" twice as many cursors
/// (white=1, green=2, blue=4, … gold=64). If even gold (tier 6) overflows, we
/// clamp to 160 gold fingers — the true rendering ceiling.</para>
/// </summary>
public static class CursorRingLayout
{
    /// <summary>Slots per concentric ring, innermost first. Widening rings hold
    /// more fingers without overlap. Sum is <see cref="MaxFingers"/>.</summary>
    public static readonly int[] RingCapacities = [18, 24, 30, 36, 52];

    /// <summary>Total on-screen finger budget (sum of <see cref="RingCapacities"/>).</summary>
    public const int MaxFingers = 160;

    /// <summary>Highest colour tier (0=white … 6=gold). Gold is the last tier;
    /// past it the ring stops growing entirely.</summary>
    public const int MaxTier = 6;

    /// <summary>Innermost ring radius, in rem — sits just outside the cookie.
    /// The cookie image is a round cookie inscribed in its 500×500 frame, drawn
    /// 15rem wide, so its edge reaches ≈7.5rem out at the four cardinal
    /// directions (top/bottom/left/right — exactly where low finger counts land).
    /// A finger box reaches ≈1.15rem inward from its anchor and the poke taps
    /// ≈0.5rem further, so the anchor must sit ≈9rem out for the fingertip to
    /// rest just past the cookie edge and only "tap" it on the poke.</summary>
    private const double BaseRadiusRem = 8.5;

    /// <summary>Radial gap between successive rings, in rem.</summary>
    private const double RingGapRem = 1.0;

    /// <summary>One rendered finger and everything the view needs to place it.</summary>
    /// <param name="GlobalIndex">Stable index across all rings; used as the render
    /// key and to stagger the poke animation phase.</param>
    /// <param name="Ring">Ring index, 0 = innermost.</param>
    /// <param name="AngleDeg">Angle around the cookie, degrees.</param>
    /// <param name="RadiusRem">Distance from centre, rem.</param>
    /// <param name="Tier">Colour tier 0..6 (all fingers share one tier).</param>
    /// <param name="Phase">Poke-animation phase offset 0..1, staggers the fingers
    /// so they tap out of sync rather than in unison.</param>
    public readonly record struct Finger(
        int GlobalIndex, int Ring, double AngleDeg, double RadiusRem, int Tier, double Phase);

    /// <summary>The whole ring's derived state for one frame.</summary>
    /// <param name="Fingers">Fingers to render (empty when no cursors owned).</param>
    /// <param name="Tier">Shared colour tier 0..6.</param>
    /// <param name="PerFinger">How many cursors each finger represents (2^Tier).</param>
    /// <param name="CursorCount">The owned cursor count this was derived from.</param>
    /// <param name="NextMergeAt">Cursor count at which the next colour merge
    /// triggers, or null when already at the gold ceiling.</param>
    public readonly record struct RingModel(
        IReadOnlyList<Finger> Fingers, int Tier, long PerFinger, long CursorCount, long? NextMergeAt);

    /// <summary>
    /// Compute the ring for a given owned cursor count. Deterministic and pure —
    /// the component calls this every render, so the ring tracks purchases live.
    /// </summary>
    public static RingModel Compute(long cursorCount)
    {
        if (cursorCount <= 0)
        {
            return new RingModel(Array.Empty<Finger>(), Tier: 0, PerFinger: 1, CursorCount: 0, NextMergeAt: MaxFingers);
        }

        // Lowest tier whose 2^tier grouping fits the count into 160 slots. Each
        // tier doubles what one finger stands for, so we climb only as far as we
        // must — keeping the ring as "white" (low tier) as possible for as long
        // as possible, matching the original's slow colour progression.
        var tier = 0;
        while (tier < MaxTier && CeilDiv(cursorCount, 1L << tier) > MaxFingers)
        {
            tier++;
        }

        var perFinger = 1L << tier;
        // At the gold ceiling this may still exceed 160 for astronomical counts;
        // clamp so the on-screen element count never passes the budget.
        var fingerCount = (int)Math.Min(MaxFingers, CeilDiv(cursorCount, perFinger));

        // The next merge happens when the count first exceeds what the current
        // tier can hold across all 160 slots (160 * 2^tier). Null once gold is
        // both reached and saturated — nothing further will ever change.
        long? nextMergeAt = null;
        if (tier < MaxTier)
        {
            nextMergeAt = (long)MaxFingers * perFinger + 1;
        }

        var fingers = BuildFingers(fingerCount, tier);
        return new RingModel(fingers, tier, perFinger, cursorCount, nextMergeAt);
    }

    /// <summary>
    /// Place <paramref name="fingerCount"/> fingers into concentric rings, filling
    /// the innermost ring before spilling outward. Each ring spreads its fingers
    /// evenly over the full circle; odd rings are offset half a step so fingers
    /// don't line up radially between rings (which reads as stiff spokes).
    /// </summary>
    private static List<Finger> BuildFingers(int fingerCount, int tier)
    {
        var fingers = new List<Finger>(fingerCount);
        var remaining = fingerCount;
        var global = 0;

        for (var ring = 0; ring < RingCapacities.Length && remaining > 0; ring++)
        {
            var n = Math.Min(remaining, RingCapacities[ring]);
            var radius = BaseRadiusRem + ring * RingGapRem;
            var step = 360.0 / n;
            // Offset alternate rings by half a slot so adjacent rings interleave.
            var offset = (ring % 2) * (step / 2.0);

            for (var i = 0; i < n; i++)
            {
                var angle = step * i + offset;
                // Phase spreads the poke over the animation period; using the
                // global index (mod a prime-ish span) keeps neighbours out of
                // sync so the tapping looks organic rather than synchronised.
                var phase = (global % 12) / 12.0;
                fingers.Add(new Finger(global, ring, angle, radius, tier, phase));
                global++;
            }

            remaining -= n;
        }

        return fingers;
    }

    // Overflow-safe ceiling division: the naive (a + b - 1) / b overflows for
    // counts near long.MaxValue, which would wrongly collapse the tier climb.
    private static long CeilDiv(long a, long b) => a / b + (a % b == 0 ? 0 : 1);
}
