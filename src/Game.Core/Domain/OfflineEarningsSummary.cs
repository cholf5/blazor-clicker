namespace Game.Core.Domain;

/// <summary>
/// Result of applying accumulated offline time when the tab reopens.
/// Handed back to the UI so it can show a "welcome back" dialog.
/// </summary>
/// <param name="Seconds">Real-time seconds credited (already capped at the max window).</param>
/// <param name="CookiesEarned">Cookies granted (already added to the state).</param>
/// <param name="Efficiency">Multiplier applied to CPS during the offline window (0..1).</param>
/// <param name="SugarLumpHarvestReady">True if a sugar lump matured during the away period.</param>
public sealed record OfflineEarningsSummary(
    double Seconds,
    double CookiesEarned,
    double Efficiency,
    bool SugarLumpHarvestReady);
