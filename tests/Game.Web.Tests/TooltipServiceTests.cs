using Game.Web.Services;
using Microsoft.AspNetCore.Components;

namespace Game.Web.Tests;

/// <summary>
/// Guards the tooltip live-refresh contract. The bug this covers: tooltips
/// froze at their mouseover-time values (owned count, CPS share, cost stopped
/// updating after buying a building) because <see cref="TooltipService"/> cached
/// a pre-built <see cref="RenderFragment"/>. The fix stores a builder invoked on
/// every <see cref="TooltipService.Content"/> access, so the host re-derives
/// live state each frame.
/// </summary>
public class TooltipServiceTests
{
    private static RenderFragment Frag() => _ => { };

    [Fact]
    public void Content_ReinvokesBuilder_OnEveryAccess()
    {
        var svc = new TooltipService();
        var calls = 0;
        svc.Show(() => { calls++; return Frag(); }, default);

        // Each read must re-run the builder — that's what lets the tooltip pick
        // up freshly-derived values (counts, share) as the game ticks, instead
        // of freezing whatever was computed at mouseover.
        _ = svc.Content;
        _ = svc.Content;
        _ = svc.Content;

        Assert.Equal(3, calls);
    }

    [Fact]
    public void Content_IsNull_WhenHidden()
    {
        var svc = new TooltipService();
        var calls = 0;
        svc.Show(() => { calls++; return Frag(); }, default);
        svc.Hide();

        Assert.Null(svc.Content);
        // A hidden tooltip must not run its builder at all.
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Show_SetsVisible_AndRaisesOnChange()
    {
        var svc = new TooltipService();
        var changes = 0;
        svc.OnChange += () => changes++;

        svc.Show(Frag, default);

        Assert.True(svc.Visible);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Hide_WhenAlreadyHidden_DoesNotRaiseOnChange()
    {
        var svc = new TooltipService();
        var changes = 0;
        svc.OnChange += () => changes++;

        svc.Hide();

        Assert.False(svc.Visible);
        Assert.Equal(0, changes);
    }
}
