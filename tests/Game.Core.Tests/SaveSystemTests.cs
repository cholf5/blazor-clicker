using Game.Core.Domain;

namespace Game.Core.Tests;

public class SaveSystemTests
{
    [Fact]
    public void SerializeDeserialize_RoundTrip_PreservesCoreState()
    {
        var s = new GameState { Cookies = 12_345.6 };
        s.BuyBuilding(BuildingId.Cursor);
        s.BuyBuilding(BuildingId.Cursor);
        s.BuyBuilding(BuildingId.Grandma);
        s.Cookies = 999_999;
        s.BuyUpgrade("tier_Cursor_1");
        s.Click();
        s.Tick(1.0);

        var json = SaveSystem.SerializeToJson(s);
        var loaded = SaveSystem.DeserializeFromJson(json);

        Assert.Equal(s.Cookies, loaded.Cookies, 4);
        Assert.Equal(s.TotalCookiesBaked, loaded.TotalCookiesBaked, 4);
        Assert.Equal(s.HandmadeClicks, loaded.HandmadeClicks);
        Assert.Equal(s.BuildingCounts[BuildingId.Cursor], loaded.BuildingCounts[BuildingId.Cursor]);
        Assert.Equal(s.BuildingCounts[BuildingId.Grandma], loaded.BuildingCounts[BuildingId.Grandma]);
        Assert.Contains("tier_Cursor_1", loaded.PurchasedUpgrades);
    }

    [Fact]
    public void ExportImport_Base64_RoundTrip()
    {
        var s = new GameState { Cookies = 7777 };
        s.BuyBuildingBulk(BuildingId.Cursor, 3);

        var exported = SaveSystem.ExportToString(s);
        Assert.False(exported.StartsWith("{")); // Base64, not raw JSON

        var loaded = SaveSystem.ImportFromString(exported);
        Assert.Equal(3, loaded.BuildingCounts[BuildingId.Cursor]);
    }

    [Fact]
    public void Import_AcceptsRawJson_Too()
    {
        var s = new GameState { Cookies = 42 };
        var json = SaveSystem.SerializeToJson(s);
        var loaded = SaveSystem.ImportFromString(json);
        Assert.Equal(42, loaded.Cookies);
    }

    [Fact]
    public void Import_EmptyString_Throws()
    {
        Assert.Throws<InvalidDataException>(() => SaveSystem.ImportFromString(""));
        Assert.Throws<InvalidDataException>(() => SaveSystem.ImportFromString("   "));
    }

    [Fact]
    public void Import_FutureVersion_Throws()
    {
        var s = new GameState();
        var data = s.ToSaveData();
        data.Version = 999;
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        Assert.Throws<InvalidDataException>(() => SaveSystem.DeserializeFromJson(json));
    }

    // Expose ToSaveData for the test above without changing production visibility.
}

file static class TestBridge
{
    // Exposes internal ToSaveData for the version-check test above via
    // InternalsVisibleTo — but simpler to just use the internal directly.
}
