using System.Globalization;

namespace Game.Core;

/// <summary>
/// Formatting helpers for the very large numbers Cookie Clicker produces.
/// Mirrors the "1.234 million / billion / trillion" style of the original.
/// </summary>
public static class NumberFormat
{
    private static readonly string[] Suffixes =
    [
        "",         // 1
        " thousand", // 1e3
        " million",  // 1e6
        " billion",  // 1e9
        " trillion", // 1e12
        " quadrillion",
        " quintillion",
        " sextillion",
        " septillion",
        " octillion",
        " nonillion",
        " decillion",
        " undecillion",
        " duodecillion",
        " tredecillion",
        " quattuordecillion",
        " quindecillion",
    ];

    /// <summary>
    /// Format a value like Cookie Clicker does — small numbers as integers,
    /// large numbers with a suffix and 3 significant decimals.
    /// </summary>
    public static string Format(double value)
    {
        if (!double.IsFinite(value)) return value.ToString(CultureInfo.InvariantCulture);
        if (value < 0) return "-" + Format(-value);
        if (value < 1_000_000) return Math.Floor(value).ToString("N0", CultureInfo.InvariantCulture);

        var tier = (int)Math.Floor(Math.Log10(value) / 3);
        if (tier <= 0) return value.ToString("N0", CultureInfo.InvariantCulture);
        if (tier >= Suffixes.Length) tier = Suffixes.Length - 1;

        var scaled = value / Math.Pow(10, tier * 3);
        return scaled.ToString("0.###", CultureInfo.InvariantCulture) + Suffixes[tier];
    }

    /// <summary>
    /// Like <see cref="Format"/>, but keeps a fixed 3-decimal width on large
    /// numbers (e.g. "1.230 million" instead of "1.23 million"). Used only for
    /// the top-of-column cookie bank, where the value refreshes every frame and
    /// a shrinking width in a centred box reads as horizontal jitter — the "0.###"
    /// mask drops trailing zeros, so "1.230 million" would collapse to "1.23
    /// million" and the number's width would flicker frame to frame.
    /// </summary>
    public static string FormatStable(double value)
    {
        if (!double.IsFinite(value)) return value.ToString(CultureInfo.InvariantCulture);
        if (value < 0) return "-" + FormatStable(-value);
        if (value < 1_000_000) return Math.Floor(value).ToString("N0", CultureInfo.InvariantCulture);

        var tier = (int)Math.Floor(Math.Log10(value) / 3);
        if (tier <= 0) return value.ToString("N0", CultureInfo.InvariantCulture);
        if (tier >= Suffixes.Length) tier = Suffixes.Length - 1;

        var scaled = value / Math.Pow(10, tier * 3);
        return scaled.ToString("0.000", CultureInfo.InvariantCulture) + Suffixes[tier];
    }

    /// <summary>
    /// Format a rate, like "12.3 million cookies per second". Behaves like
    /// <see cref="Format"/> everywhere except the sub-1 range: rates below one
    /// keep a single decimal (e.g. 0.1 for a lone Cursor) instead of being
    /// floored to "0", which is what <see cref="Format"/>'s whole-cookie policy
    /// would otherwise produce. Values in [0.1, 1) are floored to a tenth so
    /// the shown rate never exceeds the real one.
    /// </summary>
    public static string FormatRate(double value)
    {
        if (!double.IsFinite(value)) return value.ToString(CultureInfo.InvariantCulture);
        if (value < 0) return "-" + FormatRate(-value);
        if (value > 0 && value < 1)
        {
            var tenths = Math.Floor(value * 10) / 10;
            // Anything below 0.1 (e.g. 0.05) still floors to "0" — that's honest
            // and matches how the whole-cookie displays treat sub-unit values.
            return tenths.ToString("0.0", CultureInfo.InvariantCulture);
        }
        return Format(value);
    }

    /// <summary>
    /// Format a duration in seconds as a human-friendly "45s" / "12m 34s" /
    /// "1h 23m" string. Below a minute it renders whole seconds; below an hour,
    /// minutes with an optional seconds trailer; above an hour, hours with an
    /// optional minutes trailer (seconds are intentionally dropped past the
    /// minute mark — a stats readout that ticks every second is noise, not
    /// signal). Shared by <c>OfflineDialog</c> ("you were away for X") and the
    /// Stats dialog ("time played") so both use one canonical wording.
    /// </summary>
    public static string FormatDuration(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) seconds = 0;
        if (seconds < 60) return $"{(int)seconds}s";
        if (seconds < 3600)
        {
            var m = (int)(seconds / 60);
            var s = (int)(seconds % 60);
            return s > 0 ? $"{m}m {s}s" : $"{m}m";
        }
        var h = (int)(seconds / 3600);
        var mm = (int)((seconds % 3600) / 60);
        return mm > 0 ? $"{h}h {mm}m" : $"{h}h";
    }
}
