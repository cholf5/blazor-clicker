namespace Game.Core.Domain;

/// <summary>
/// Effect a golden cookie applies when clicked.
/// </summary>
public enum GoldenCookieEffect
{
    /// <summary>Instantly grants a lump of cookies (min of a % of bank vs a fixed CPS window).</summary>
    Lucky,

    /// <summary>Temporarily multiplies global CPS.</summary>
    Frenzy,

    /// <summary>Temporarily multiplies click power.</summary>
    ClickFrenzy,
}

/// <summary>
/// A golden cookie currently spawned on screen. Immutable — replaced on
/// mutation to keep change-detection cheap in Blazor.
/// </summary>
public sealed record GoldenCookie(
    Guid Id,
    GoldenCookieEffect Effect,
    double SpawnedAt,       // wall-clock seconds since state creation
    double ExpiresAt,       // seconds after which it despawns automatically
    double ScreenX,         // 0..1 relative position
    double ScreenY);        // 0..1 relative position

/// <summary>
/// An active temporary buff (Frenzy, Click frenzy, …). Expires when the
/// game's clock passes <see cref="ExpiresAt"/>.
/// </summary>
public sealed record ActiveBuff(
    GoldenCookieEffect Effect,
    double Multiplier,
    double ExpiresAt);
