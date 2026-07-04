using Game.Core.Domain;

namespace Game.Core;

/// <summary>
/// Serializable snapshot of a <see cref="GameState"/>. This is the on-disk /
/// LocalStorage schema — treat it as append-only and always migrate through
/// <see cref="SaveSystem"/> when the shape changes.
/// </summary>
public sealed class SaveData
{
    /// <summary>Increment when the shape of <see cref="SaveData"/> changes.</summary>
    public int Version { get; set; } = 1;

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
}
