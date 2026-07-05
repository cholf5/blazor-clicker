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
}
