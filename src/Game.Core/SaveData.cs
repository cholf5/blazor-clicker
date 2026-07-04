using Game.Core.Domain;

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
    /// </summary>
    public int Version { get; set; } = 3;

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

    /// <summary>Total sugar lumps harvested over the lifetime of this save.</summary>
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
}
