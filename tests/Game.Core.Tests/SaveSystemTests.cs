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

    [Fact]
    public void Migrate_V1Save_UpgradesToCurrentAndSeedsLifetimeBaked()
    {
        // Hand-rolled v1 save: only has the fields that existed at v1. The
        // migration must preserve them, seed AllTimeCookiesBaked from the
        // per-run counter, and leave the newer fields at their defaults.
        var v1Json = """
        {
          "Version": 1,
          "Cookies": 500,
          "TotalCookiesBaked": 1234,
          "HandmadeClicks": 10,
          "GoldenCookiesClicked": 0,
          "GameTime": 42.5,
          "BuildingCounts": { "Cursor": 3 },
          "PurchasedUpgrades": [],
          "UnlockedAchievements": [],
          "Buffs": [],
          "ActiveGolden": null,
          "NextGoldenAt": 100
        }
        """;

        var state = SaveSystem.DeserializeFromJson(v1Json, out var savedAt);
        Assert.Equal(0, savedAt); // v1 has no timestamp
        Assert.Equal(500, state.Cookies);
        Assert.Equal(1234, state.TotalCookiesBaked);
        Assert.Equal(1234, state.AllTimeCookiesBaked); // seeded from run baked
        Assert.Equal(3, state.BuildingCounts[BuildingId.Cursor]);
        Assert.Equal(0, state.PrestigeLevel);
        Assert.Equal(0, state.SugarLumps);
        Assert.False(state.SugarLumpReady);
        // v3 counters have no v1 source and default to zero.
        Assert.Equal(0, state.HandmadeCookies);
        Assert.Equal(0, state.GoldenClicksDuringFrenzy);
    }

    [Fact]
    public void RoundTrip_PreservesV3Counters()
    {
        var s = new GameState();
        for (var i = 0; i < 5; i++) s.Click(); // accrues HandmadeCookies
        var json = SaveSystem.SerializeToJson(s);
        var loaded = SaveSystem.DeserializeFromJson(json);

        Assert.Equal(s.HandmadeCookies, loaded.HandmadeCookies, 4);
        Assert.Equal(s.GoldenClicksDuringFrenzy, loaded.GoldenClicksDuringFrenzy);
    }

    [Fact]
    public void Serialize_StampsSavedAtUnixSeconds()
    {
        var s = new GameState { Cookies = 1 };
        var json = SaveSystem.SerializeToJson(s);
        SaveSystem.DeserializeFromJson(json, out var savedAt);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.InRange(savedAt, now - 10, now + 10);
    }

    [Fact]
    public void Migrate_V3Save_LeavesLanguageNull()
    {
        // A v3 save has no Language field; migration to v4 must leave the
        // chosen language null so the player follows the system language.
        var v3Json = """
        {
          "Version": 3,
          "Cookies": 10,
          "TotalCookiesBaked": 10,
          "AllTimeCookiesBaked": 10,
          "BuildingCounts": {},
          "PurchasedUpgrades": [],
          "UnlockedAchievements": []
        }
        """;

        var state = SaveSystem.DeserializeFromJson(v3Json);
        Assert.Null(state.ChosenLanguage);
    }

    [Fact]
    public void RoundTrip_PreservesChosenLanguage()
    {
        var s = new GameState { Cookies = 1, ChosenLanguage = Game.Core.Localization.Language.TraditionalChinese };
        var json = SaveSystem.SerializeToJson(s);
        var loaded = SaveSystem.DeserializeFromJson(json);
        Assert.Equal(Game.Core.Localization.Language.TraditionalChinese, loaded.ChosenLanguage);
    }

    [Fact]
    public void Migrate_V4Save_KeepsSugarLumpsAsBalanceAndEmptyBuildingLevels()
    {
        // A v4 save's SugarLumps counted "harvested lumps = +1% global each".
        // v5 reinterprets the same number as an unspent balance (strategy A),
        // so the player loses nothing and BuildingLevels starts empty.
        var v4Json = """
        {
          "Version": 4,
          "Cookies": 10,
          "TotalCookiesBaked": 10,
          "AllTimeCookiesBaked": 10,
          "SugarLumps": 7,
          "BuildingCounts": {},
          "PurchasedUpgrades": [],
          "UnlockedAchievements": []
        }
        """;

        var state = SaveSystem.DeserializeFromJson(v4Json);
        Assert.Equal(7, state.SugarLumps);
        Assert.Empty(state.BuildingLevels);
    }

    [Fact]
    public void RoundTrip_PreservesBuildingLevels()
    {
        var s = new GameState();
        typeof(GameState).GetProperty("SugarLumps")!.SetValue(s, 10L);
        s.LevelUpBuilding(BuildingId.Grandma);
        s.LevelUpBuilding(BuildingId.Grandma);
        var json = SaveSystem.SerializeToJson(s);
        var loaded = SaveSystem.DeserializeFromJson(json);
        Assert.Equal(2, loaded.BuildingLevels[BuildingId.Grandma]);
        Assert.Equal(s.SugarLumps, loaded.SugarLumps);
    }

    [Fact]
    public void BuyUpgrade_StampsPurchaseTime()
    {
        var s = new GameState { Cookies = 1_000_000 };
        s.BuyBuilding(BuildingId.Cursor);
        s.Tick(3.0); // advance GameTime so the stamp is non-zero

        Assert.True(s.BuyUpgrade("tier_Cursor_1"));

        Assert.True(s.UpgradePurchaseTimes.ContainsKey("tier_Cursor_1"));
        Assert.Equal(s.GameTime, s.UpgradePurchaseTimes["tier_Cursor_1"], 4);
    }

    [Fact]
    public void Ascend_ClearsUpgradePurchaseTimesAlongsideUpgrades()
    {
        var s = new GameState { Cookies = 1e15 };
        s.BuyBuilding(BuildingId.Cursor);
        s.BuyUpgrade("tier_Cursor_1");
        Assert.NotEmpty(s.UpgradePurchaseTimes);

        // Bake enough for at least one prestige level.
        typeof(GameState).GetProperty("TotalCookiesBaked")!.SetValue(s, 1e15);
        Assert.True(s.Ascend());

        Assert.Empty(s.PurchasedUpgrades);
        Assert.Empty(s.UpgradePurchaseTimes);
    }

    [Fact]
    public void RoundTrip_PreservesUpgradePurchaseTimes()
    {
        var s = new GameState { Cookies = 1_000_000 };
        s.BuyBuilding(BuildingId.Cursor);
        s.Tick(1.25);
        s.BuyUpgrade("tier_Cursor_1");
        s.Tick(2.5);
        s.BuyBuilding(BuildingId.Grandma);
        s.Cookies = 1_000_000;
        s.BuyUpgrade("tier_Grandma_1");

        var json = SaveSystem.SerializeToJson(s);
        var loaded = SaveSystem.DeserializeFromJson(json);

        Assert.Equal(s.UpgradePurchaseTimes["tier_Cursor_1"], loaded.UpgradePurchaseTimes["tier_Cursor_1"], 4);
        Assert.Equal(s.UpgradePurchaseTimes["tier_Grandma_1"], loaded.UpgradePurchaseTimes["tier_Grandma_1"], 4);
        // The two purchases happened at different game times — the ordering
        // (which is what the "sort by recent" UI relies on) must survive.
        Assert.True(loaded.UpgradePurchaseTimes["tier_Grandma_1"]
                    > loaded.UpgradePurchaseTimes["tier_Cursor_1"]);
    }

    [Fact]
    public void Migrate_V5Save_BackfillsUpgradePurchaseTimesFromGameTime()
    {
        // A v5 save has no UpgradePurchaseTimes; the migration must backfill
        // every already-owned upgrade with the save's GameTime so the Stats
        // dialog's "recent first" ordering has a stable base.
        var v5Json = """
        {
          "Version": 5,
          "Cookies": 100,
          "TotalCookiesBaked": 100,
          "AllTimeCookiesBaked": 100,
          "GameTime": 512.5,
          "BuildingCounts": {},
          "PurchasedUpgrades": ["tier_Cursor_1", "tier_Grandma_1"],
          "UnlockedAchievements": []
        }
        """;

        var state = SaveSystem.DeserializeFromJson(v5Json);
        Assert.Equal(2, state.UpgradePurchaseTimes.Count);
        Assert.Equal(512.5, state.UpgradePurchaseTimes["tier_Cursor_1"], 4);
        Assert.Equal(512.5, state.UpgradePurchaseTimes["tier_Grandma_1"], 4);
    }

    // Expose ToSaveData for the test above without changing production visibility.
}

file static class TestBridge
{
    // Exposes internal ToSaveData for the version-check test above via
    // InternalsVisibleTo — but simpler to just use the internal directly.
}
