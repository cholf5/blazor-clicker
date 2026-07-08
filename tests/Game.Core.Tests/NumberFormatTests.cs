using Game.Core;

namespace Game.Core.Tests;

public class NumberFormatTests
{
    [Fact]
    public void FormatStable_KeepsTrailingZero()
    {
        // The whole point: "0.###" would collapse this to "1.23 million" and the
        // centred bank number would flicker as its width shrank.
        Assert.Equal("1.230 million", NumberFormat.FormatStable(1_230_000));
    }

    [Fact]
    public void FormatStable_ShowsThreeDecimals()
    {
        Assert.Equal("1.234 million", NumberFormat.FormatStable(1_234_000));
    }

    [Fact]
    public void FormatStable_BelowMillion_HasNoDecimals()
    {
        Assert.Equal("999,999", NumberFormat.FormatStable(999_999));
    }

    [Fact]
    public void FormatStable_Negative_KeepsSign()
    {
        Assert.Equal("-1.230 million", NumberFormat.FormatStable(-1_230_000));
    }

    [Fact]
    public void Format_StillDropsTrailingZero()
    {
        // Guard that FormatStable was added without altering the original Format,
        // whose "省略末尾 0" style is intentional everywhere else in the UI.
        Assert.Equal("1.23 million", NumberFormat.Format(1_230_000));
    }

    [Fact]
    public void Format_FloorsSubUnitValues()
    {
        // Whole-cookie displays (bank, totals, costs) intentionally floor: you
        // don't have "0.3 cookies in the bank". FormatRate is the exception.
        Assert.Equal("0", NumberFormat.Format(0.1));
    }

    [Fact]
    public void FormatRate_KeepsOneDecimalBelowOne()
    {
        // The whole point of FormatRate: a single Cursor produces 0.1 CPS, and
        // showing that as "0" — as Format would — makes the stats panel and the
        // Cursor tooltip look broken right after the first purchase.
        Assert.Equal("0.1", NumberFormat.FormatRate(0.1));
        Assert.Equal("0.5", NumberFormat.FormatRate(0.5));
        Assert.Equal("0.9", NumberFormat.FormatRate(0.9));
    }

    [Fact]
    public void FormatRate_FloorsWithinTheTenths()
    {
        // Floor (not round) so the shown rate never overstates the real one —
        // matches how Format treats the whole-cookie ranges.
        Assert.Equal("0.1", NumberFormat.FormatRate(0.19));
    }

    [Fact]
    public void FormatRate_BelowTenth_StillZero()
    {
        // "0.0X" is honestly zero at one-decimal precision; keeping it as "0"
        // avoids a fake "0.0" display before the player owns anything.
        Assert.Equal("0", NumberFormat.FormatRate(0));
        Assert.Equal("0.0", NumberFormat.FormatRate(0.05));
    }

    [Fact]
    public void FormatRate_AtOrAboveOne_MatchesFormat()
    {
        // Above 1 CPS the rate is a whole-number-ish display just like Format:
        // the sub-1 special case must not leak into normal ranges.
        Assert.Equal("1", NumberFormat.FormatRate(1.0));
        Assert.Equal("42", NumberFormat.FormatRate(42.7));
        Assert.Equal("1.23 million", NumberFormat.FormatRate(1_230_000));
    }

    [Fact]
    public void FormatRate_Negative_KeepsSign()
    {
        Assert.Equal("-0.1", NumberFormat.FormatRate(-0.1));
    }
}
