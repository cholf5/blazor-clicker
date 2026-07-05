using Microsoft.AspNetCore.Components;

namespace Game.Web.Services;

/// <summary>
/// Holds the single, app-wide tooltip state. Any component can push rich
/// content via <see cref="Show"/> (typically on mouseover) and clear it with
/// <see cref="Hide"/> (on mouseout). The lone <c>TooltipHost</c> component
/// subscribes to <see cref="OnChange"/> and renders the content in a
/// top-level fixed layer, so tooltips never get clipped by scroll containers.
///
/// Registered as Scoped, which in Blazor WebAssembly is effectively a
/// singleton for the app's lifetime — matching the other game services.
/// </summary>
public sealed class TooltipService
{
    /// <summary>
    /// Builds the fragment on demand. Stored as a factory (not a baked
    /// <see cref="RenderFragment"/>) so the content re-derives live values —
    /// owned counts, CPS share, cost — every time the host re-renders, instead
    /// of freezing whatever they were at mouseover time.
    /// </summary>
    private Func<RenderFragment>? _builder;

    /// <summary>The fragment to render, or null when nothing is shown.</summary>
    public RenderFragment? Content => Visible ? _builder?.Invoke() : null;

    /// <summary>
    /// The element the tooltip is anchored to. The host reads its bounding
    /// rect to place the card beside it (original-game style), so the tooltip
    /// stays put while hovering instead of chasing the cursor.
    /// </summary>
    public ElementReference Anchor { get; private set; }

    /// <summary>Whether a tooltip is currently visible.</summary>
    public bool Visible { get; private set; }

    /// <summary>Raised whenever <see cref="Content"/> or <see cref="Visible"/> changes.</summary>
    public event Action? OnChange;

    /// <summary>Show the given content anchored to <paramref name="anchor"/>.</summary>
    /// <param name="content">
    /// A factory invoked on every render so the tooltip reflects current state.
    /// Callers pass a lambda that recomputes its values, not a pre-built fragment.
    /// </param>
    public void Show(Func<RenderFragment> content, ElementReference anchor)
    {
        _builder = content;
        Anchor = anchor;
        Visible = true;
        OnChange?.Invoke();
    }

    /// <summary>Hide the active tooltip. Content is kept until the next Show.</summary>
    public void Hide()
    {
        if (!Visible) return;
        Visible = false;
        OnChange?.Invoke();
    }
}
