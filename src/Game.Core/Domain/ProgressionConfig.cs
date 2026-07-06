namespace Game.Core.Domain;

/// <summary>
/// Tunable numeric parameters for late-game systems (sugar lumps, prestige,
/// offline earnings). Grouped in one place so balance changes are auditable.
/// </summary>
public static class ProgressionConfig
{
    // ---- Sugar lumps -------------------------------------------------------
    /// <summary>
    /// Wall-clock seconds a fresh sugar lump takes to ripen. Aligned with Cookie
    /// Clicker's canonical value (~23 hours) rather than a shortened one: per
    /// ADR 0006, sugar lumps are a <b>flavour</b> system, not a growth source, and
    /// the ripen time is the primary lever that keeps their magnitude tiny. A ~1/day
    /// cadence makes each lump scarce enough that "which building do I feed?" is a
    /// real decision. See ADR 0006 §2–§3 for why the earlier 30-minute value was wrong.
    /// </summary>
    public const double SugarLumpRipenSeconds = 24 * 60 * 60; // 24 hours (~1 per day)

    /// <summary>
    /// CPS bonus granted per sugar-lump level spent on a building, additive within
    /// that single building (level 10 == that building ×1.10). Does <b>not</b> touch
    /// the global multiplier — see <see cref="GameState.GetBuildingUnitCps"/>.
    /// </summary>
    public const double SugarLumpBuildingLevelBonus = 0.01; // +1% per level, per building

    /// <summary>Total cookies baked (in the current run) required before the first sugar lump seed even appears.</summary>
    public const double SugarLumpUnlockThreshold = 1_000_000_000; // 1 billion

    // ---- Milk --------------------------------------------------------------
    /// <summary>
    /// Milk gained per achievement unlocked, expressed as a fraction where
    /// 1.0 == "100% milk". Matches Cookie Clicker's canonical +4% per achievement.
    ///
    /// Milk is a purely <b>passive, derived</b> quantity — it has no state of its
    /// own (see <see cref="GameState.MilkFactor"/>, computed from the achievement
    /// count) and demands no active management, so it stays inside the pure-growth
    /// scope (ADR 0004) unlike Wrinklers/Garden. On its own it does nothing; the
    /// Kitten upgrades (<see cref="UpgradeEffectKind.KittenMilkMultiplier"/>) turn
    /// it into a global CPS multiplier, which is what keeps late-game growth from
    /// stalling once the fixed multipliers are exhausted.
    /// </summary>
    public const double MilkPerAchievement = 0.04; // +4% milk per achievement

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
