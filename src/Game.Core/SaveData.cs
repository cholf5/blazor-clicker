using Game.Core.Domain;
using Game.Core.Localization;

namespace Game.Core;

/// <summary>
/// Serializable snapshot of a <see cref="GameState"/>. This is the on-disk /
/// LocalStorage schema — treat it as append-only and always migrate through
/// <see cref="SaveSystem"/> when the shape changes.
/// </summary>
public sealed class SaveData
{
    /// <summary>
    /// Increment when the shape of <see cref="SaveData"/> changes.
    ///
    /// * v1 — initial (M1–M4): buildings, upgrades, achievements, golden cookies.
    /// * v2 — added sugar lumps, prestige/ascension, offline earnings timestamp,
    ///   and lifetime baked counter. Older saves default the new fields to 0 /
    ///   false / current game time.
    /// * v3 — added handmade-cookie and frenzy-combo counters for the extra
    ///   achievement families. Older saves default both to 0.
    /// * v4 — added the player's chosen display language. Nullable: null means
    ///   "never chosen" (follow the system/browser language), a value means the
    ///   player picked it explicitly. Older saves migrate to null.
    /// * v5 — sugar lumps became spendable. <see cref="SugarLumps"/> changed
    ///   meaning from "lumps harvested = permanent +1% global each" to "unspent
    ///   balance", and <see cref="BuildingLevels"/> was added (levels bought with
    ///   lumps, +1% per level to that one building). Older saves keep their lump
    ///   count as the starting balance and default BuildingLevels to empty.
    /// * v6 — added <see cref="UpgradePurchaseTimes"/>: the simulated
    ///   <see cref="Domain.GameState.GameTime"/> at which each purchased upgrade
    ///   was bought, so the Stats dialog can offer a "sort by recent purchase"
    ///   view. Pre-v6 saves migrate by backfilling every already-owned upgrade
    ///   with the save's current GameTime — imprecise but monotonic (everything
    ///   bought pre-migration clusters at load time; subsequent purchases push
    ///   above it in order).
    /// </summary>
    public int Version { get; set; } = 6;

    // ---- v1 fields ---------------------------------------------------------
    public double Cookies { get; set; }
    public double TotalCookiesBaked { get; set; }
    public long HandmadeClicks { get; set; }
    public long GoldenCookiesClicked { get; set; }
    public double GameTime { get; set; }
    public Dictionary<BuildingId, int> BuildingCounts { get; set; } = new();
    public List<string> PurchasedUpgrades { get; set; } = new();
    public List<string> UnlockedAchievements { get; set; } = new();
    public List<ActiveBuff> Buffs { get; set; } = new();
    public GoldenCookie? ActiveGolden { get; set; }
    public double NextGoldenAt { get; set; }

    // ---- v2 additions ------------------------------------------------------
    /// <summary>Cookies baked across every run this save has ever done. Never resets.</summary>
    public double AllTimeCookiesBaked { get; set; }

    /// <summary>Number of times the player ascended. 0 for a fresh save.</summary>
    public int AscensionCount { get; set; }

    /// <summary>Prestige levels accumulated across all ascensions.</summary>
    public int PrestigeLevel { get; set; }

    /// <summary>
    /// Unspent sugar lumps. Up to and including v4 this counted lumps harvested and
    /// each granted a permanent +1% global CPS bonus; from v5 it is a spendable
    /// balance (see <see cref="BuildingLevels"/>). The v4→v5 migration keeps the
    /// stored number as the opening balance.
    /// </summary>
    public long SugarLumps { get; set; }

    /// <summary>Whether a sugar lump is currently ripe and waiting to be harvested.</summary>
    public bool SugarLumpReady { get; set; }

    /// <summary>Game time (seconds) at which the next sugar lump will ripen.</summary>
    public double SugarLumpNextAt { get; set; }

    /// <summary>
    /// Unix seconds when the save was last written to disk. Used to compute
    /// offline earnings when the tab reopens. Zero on saves that pre-date v2.
    /// </summary>
    public long SavedAtUnixSeconds { get; set; }

    // ---- v3 additions ------------------------------------------------------
    /// <summary>Cookies produced by manual clicks only. Never resets.</summary>
    public double HandmadeCookies { get; set; }

    /// <summary>Golden cookies clicked while a frenzy-type buff was already active.</summary>
    public long GoldenClicksDuringFrenzy { get; set; }

    // ---- v4 additions ------------------------------------------------------
    /// <summary>
    /// The language the player explicitly chose, or null to follow the system /
    /// browser language. Null on saves that pre-date v4.
    /// </summary>
    public Language? Language { get; set; }

    // ---- v5 additions ------------------------------------------------------
    /// <summary>
    /// Sugar-lump levels invested per building (+1% production each, to that
    /// building only). Empty on saves that pre-date v5.
    /// </summary>
    public Dictionary<BuildingId, int> BuildingLevels { get; set; } = new();

    // ---- v6 additions ------------------------------------------------------
    /// <summary>
    /// Simulated <see cref="Domain.GameState.GameTime"/> (seconds) at which each
    /// upgrade in <see cref="PurchasedUpgrades"/> was bought. Powers the Stats
    /// dialog's "sort by recent purchase" view. Empty on saves that pre-date
    /// v6; the v5→v6 migration backfills every already-owned upgrade with the
    /// save's current GameTime so old entries cluster at load time and new
    /// purchases sort above them.
    /// </summary>
    public Dictionary<string, double> UpgradePurchaseTimes { get; set; } = new();
}
