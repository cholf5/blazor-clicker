namespace Game.Core.Domain;

/// <summary>
/// Tunable numeric parameters for late-game systems (sugar lumps, prestige,
/// offline earnings). Grouped in one place so balance changes are auditable.
/// </summary>
public static class ProgressionConfig
{
    // ---- Sugar lumps -------------------------------------------------------
    /// <summary>
    /// Wall-clock seconds a fresh sugar lump takes to ripen. Cookie Clicker
    /// canonical value is 23 hours; we shorten it since this fan remake is
    /// intended for shorter sessions.
    /// </summary>
    public const double SugarLumpRipenSeconds = 30 * 60; // 30 minutes

    /// <summary>Extra CPS multiplier granted per sugar lump harvested (multiplicative on top of raw CPS).</summary>
    public const double SugarLumpCpsBonus = 0.01; // +1% per lump

    /// <summary>Total cookies baked (in the current run) required before the first sugar lump seed even appears.</summary>
    public const double SugarLumpUnlockThreshold = 1_000_000_000; // 1 billion

    // ---- Prestige / ascension ---------------------------------------------
    /// <summary>Cookies needed (per prestige level) — level = floor(cbrt(bakedThisRun / this)).</summary>
    public const double PrestigeCubeUnit = 1_000_000_000_000d; // 1 trillion

    /// <summary>CPS multiplier granted per prestige level (added into global mult).</summary>
    public const double PrestigeCpsBonus = 0.02; // +2% per level

    /// <summary>Minimum cookies baked this run before ascending is even offered.</summary>
    public const double MinCookiesToAscend = 1_000_000_000_000d;

    // ---- Building unlock ---------------------------------------------------
    /// <summary>
    /// A building becomes visible/purchasable once this run's TotalCookiesBaked
    /// reaches its BaseCost times this fraction (and the previous building is
    /// owned). Slightly below 1 so the next building reveals just as the player
    /// is about to afford it, mirroring the original's "appears when you're
    /// close" feel.
    /// </summary>
    public const double BuildingUnlockCostFraction = 0.5;

    // ---- Offline earnings --------------------------------------------------
    /// <summary>Maximum real-time window (seconds) that can accumulate offline. Longer waits are truncated.</summary>
    public const double OfflineMaxSeconds = 24 * 60 * 60; // 24 hours

    /// <summary>Efficiency the CPS runs at while offline (0..1). Vanilla is 50%.</summary>
    public const double OfflineEfficiency = 0.5;

    /// <summary>Minimum offline seconds before the game bothers to show a welcome-back summary.</summary>
    public const double OfflineMinReportSeconds = 30;
}
