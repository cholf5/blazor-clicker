using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Web.Components;

/// <summary>
/// Pure, testable layout math for the decorative cursor ring drawn behind the
/// big cookie. Given the number of owned Cursor buildings, it produces the list
/// of fingers to render — their ring, angle, radius and colour tier.
///
/// <para><b>Why a fixed on-screen budget.</b> Each finger is a live-animated DOM
/// element; unlike the original game's single &lt;canvas&gt;, DOM layout/paint
/// cost grows with element count regardless of how fast WASM runs. So the ring
/// holds at most a handful of concentric rings whose per-ring slot counts live
/// in the single source of truth <see cref="RingCapacities"/>; their sum is
/// <see cref="MaxFingers"/>. Growth beyond that is expressed by <i>colour</i>,
/// not more elements: once the rings are full we pick the lowest colour tier T
/// such that ceil(count / 2^T) still fits in <see cref="MaxFingers"/> slots, and
/// draw that many fingers all at tier T. Each tier up means every finger
/// "represents" twice as many cursors (white=1, green=2, blue=4, … gold=64). If
/// even gold (tier 6) overflows, we clamp to <see cref="MaxFingers"/> gold
/// fingers — the true rendering ceiling.</para>
/// </summary>
public static class CursorRingLayout
{
    /// <summary>Slots per concentric ring, innermost first. Widening rings hold
    /// more fingers without overlap. This is the <b>single source of truth</b>
    /// for the ring shape: the on-screen budget (<see cref="MaxFingers"/>) and
    /// every colour-merge threshold are derived from it, so tuning a ring's
    /// finger count here (or adding/removing a ring) automatically re-derives the
    /// merge maths and the tests — nothing else hard-codes these numbers.
    ///
    /// <para>A <c>static readonly</c> field now the values are settled. While
    /// tuning it live it was briefly an expression-bodied property so
    /// <c>dotnet watch</c> Hot Reload could re-apply edits (a static field
    /// initializer only runs once in the static constructor, which Hot Reload
    /// never re-executes). That's a needless per-access array allocation once the
    /// numbers are final — restore the property form if you need to hot-tune these
    /// again.</para></summary>
    public static readonly int[] RingCapacities = [30, 36, 42, 48, 54];

    /// <summary>Total on-screen finger budget — the sum of
    /// <see cref="RingCapacities"/>, computed once so it can never drift from the
    /// array. Every merge threshold keys off this, so changing the array reshapes
    /// the whole progression.</summary>
    public static readonly int MaxFingers = RingCapacities.Sum();

    /// <summary>Highest colour tier (0=white … 6=gold). Gold is the last tier;
    /// past it the ring stops growing entirely.</summary>
    public const int MaxTier = 6;

    /// <summary>Innermost ring radius, in rem — sits just outside the cookie.
    /// The cookie image is a round cookie inscribed in its 500×500 frame, drawn
    /// 15rem wide, so its edge reaches ≈7.5rem out at the four cardinal
    /// directions (top/bottom/left/right — exactly where low finger counts land).
    /// A finger box reaches ≈0.95rem inward from its anchor and the poke taps
    /// ≈0.4rem further, so the anchor must sit ≈8rem out for the fingertip to
    /// rest just past the cookie edge and only "tap" it on the poke.</summary>
    private const double BaseRadiusRem = 8.4;

    /// <summary>Radial gap between successive rings, in rem. Paired with the
    /// shrunk ≈1.9rem-tall finger box and denser per-ring slot counts so
    /// neighbouring rings stay visually distinct without pushing the outermost
    /// ring off-screen.</summary>
    private const double RingGapRem = 1.6;

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

        // Lowest tier whose 2^tier grouping fits the count into MaxFingers slots.
        // Each tier doubles what one finger stands for, so we climb only as far as
        // we must — keeping the ring as "white" (low tier) as possible for as long
        // as possible, matching the original's slow colour progression.
        var tier = 0;
        while (tier < MaxTier && CeilDiv(cursorCount, 1L << tier) > MaxFingers)
        {
            tier++;
        }

        var perFinger = 1L << tier;
        // At the gold ceiling this may still exceed MaxFingers for astronomical
        // counts; clamp so the on-screen element count never passes the budget.
        var fingerCount = (int)Math.Min(MaxFingers, CeilDiv(cursorCount, perFinger));

        // The next merge happens when the count first exceeds what the current
        // tier can hold across all slots (MaxFingers * 2^tier). Null once gold is
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
    /// the innermost ring before spilling outward. Fingers within a ring are
    /// <b>adjacent</b> — spaced by the ring's slot step (<c>360 / capacity</c>)
    /// and centred around angle 0 (the top of the cookie), so a small queue reads
    /// as "growing outward from the top" rather than being sprayed evenly around
    /// the whole circle. Buying a cursor drops one more finger onto the end of
    /// the queue without moving the ones already there, which is the whole point
    /// of the ring as purchase feedback — you can see the newcomer.
    ///
    /// <para>Once a ring is full (n == capacity) the angles wrap the entire
    /// circle and the layout is indistinguishable from a uniform ring, which is
    /// the natural "the ring is full" reading. Alternate rings still take a
    /// half-step interleave so their fingers don't line up radially with the
    /// ring inside them (which reads as stiff spokes).</para>
    /// </summary>
    private static List<Finger> BuildFingers(int fingerCount, int tier)
    {
        var fingers = new List<Finger>(fingerCount);
        var remaining = fingerCount;
        var global = 0;

        var capacities = RingCapacities;

        for (var ring = 0; ring < capacities.Length && remaining > 0; ring++)
        {
            var capacity = capacities[ring];
            var n = Math.Min(remaining, capacity);
            var radius = BaseRadiusRem + ring * RingGapRem;
            // Step is fixed by the ring's capacity, NOT by the current fill n.
            // That's what lets a partial ring feel like the head of a queue: as
            // n grows, existing fingers stay put and the newcomers slot in on
            // the ends. If step were 360/n instead, every purchase would rotate
            // every finger — which is what the previous "evenly spaced" layout
            // did, and what this rewrite is fixing.
            var step = 360.0 / capacity;
            // Half-a-step interleave on odd rings so rings 0/1, 2/3, … don't
            // form radial spokes where their fingertips would otherwise align.
            var interleave = (ring % 2) * (step / 2.0);
            // Centre the (possibly partial) fill around angle 0 (north): with
            // n fingers spaced by step, positions are (i - (n-1)/2) * step, so
            // n=1 → {0°}, n=2 → {-step/2, +step/2}, n=3 → {-step, 0, +step}, …
            var startOffset = -(n - 1) * step / 2.0;

            for (var i = 0; i < n; i++)
            {
                var angle = startOffset + step * i + interleave;
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
