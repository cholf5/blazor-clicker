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

    /// <summary>Format a rate, like "12.3 million cookies per second".</summary>
    public static string FormatRate(double value) => Format(value);
}
