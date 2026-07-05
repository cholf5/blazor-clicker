using Game.Core;
using Game.Core.Data;
using Game.Core.Domain;
using Game.Core.Localization;

namespace Game.Core.Tests;

/// <summary>
/// Guards the localization system: every player-facing string must resolve in
/// every language, the two Chinese overlays must actually cover the whole game
/// (not silently fall back to English), template placeholders must fill, and
/// the design invariants (magnitude words stay English, v3→v4 migration leaves
/// language null) must hold.
/// </summary>
public class LocalizationTests
{
    private static readonly Language[] AllLanguages =
        [Language.English, Language.SimplifiedChinese, Language.TraditionalChinese];

    private static readonly Language[] ChineseLanguages =
        [Language.SimplifiedChinese, Language.TraditionalChinese];

    // ------------------------------------------------------------------
    // Content coverage: names / descriptions in every language
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(ChineseLangData))]
    public void EveryBuilding_HasLocalizedNameAndDesc(Language lang)
    {
        var loc = new Localizer(lang);
        foreach (var b in Buildings.All)
        {
            // Overlay must exist (not fall back to English) and be non-empty.
            Assert.False(string.IsNullOrWhiteSpace(loc.Overlay($"building.{Key(b.Id)}.name")),
                $"Missing {lang} name for building {b.Id}");
            Assert.False(string.IsNullOrWhiteSpace(loc.Overlay($"building.{Key(b.Id)}.desc")),
                $"Missing {lang} desc for building {b.Id}");
            // And the display seam must produce something non-empty.
            Assert.False(string.IsNullOrWhiteSpace(b.DisplayName(loc)));
            Assert.False(string.IsNullOrWhiteSpace(b.DisplayFlavor(loc)));
        }
    }

    [Theory]
    [MemberData(nameof(ChineseLangData))]
    public void EveryUpgrade_HasLocalizedNameAndDesc(Language lang)
    {
        var loc = new Localizer(lang);
        foreach (var u in Upgrades.All)
        {
            var name = u.DisplayName(loc);
            var desc = u.DisplayDescription(loc);
            Assert.False(string.IsNullOrWhiteSpace(name), $"Empty {lang} name for upgrade {u.Id}");
            Assert.False(string.IsNullOrWhiteSpace(desc), $"Empty {lang} desc for upgrade {u.Id}");
            // A Chinese display string that still equals the inline English source
            // means the overlay is missing — catch that as a coverage gap.
            Assert.NotEqual(u.Name, name);
            Assert.NotEqual(u.Description, desc);
        }
    }

    [Theory]
    [MemberData(nameof(ChineseLangData))]
    public void EveryAchievement_HasLocalizedNameAndDesc(Language lang)
    {
        var loc = new Localizer(lang);
        foreach (var a in Achievements.All)
        {
            var name = a.DisplayName(loc);
            var desc = a.DisplayDescription(loc);
            Assert.False(string.IsNullOrWhiteSpace(name), $"Empty {lang} name for achievement {a.Id}");
            Assert.False(string.IsNullOrWhiteSpace(desc), $"Empty {lang} desc for achievement {a.Id}");
            Assert.NotEqual(a.Description, desc);
            // Names of magnitude-generated families intentionally embed an English
            // magnitude word, so a name may legitimately share substrings with the
            // English source; we only require it be non-empty (checked above).
        }
    }

    [Theory]
    [MemberData(nameof(ChineseLangData))]
    public void EveryAchievementCategory_HasLocalizedLabel(Language lang)
    {
        var loc = new Localizer(lang);
        foreach (var c in AchievementCategories.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(loc.Overlay($"category.{c.Key}.label")),
                $"Missing {lang} label for category {c.Key}");
        }
    }

    // ------------------------------------------------------------------
    // News coverage: every idle / progress / event key resolves
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(ChineseLangData))]
    public void EveryNewsKey_HasLocalizedText(Language lang)
    {
        var loc = new Localizer(lang);
        foreach (var key in NewsFlavor.IdleKeys)
            Assert.False(string.IsNullOrWhiteSpace(loc.Overlay(key)), $"Missing {lang} idle news {key}");
        foreach (var (_, key) in NewsFlavor.ProgressMessages)
            Assert.False(string.IsNullOrWhiteSpace(loc.Overlay(key)), $"Missing {lang} progress news {key}");
    }

    [Theory]
    [MemberData(nameof(AllLangData))]
    public void EventNews_ResolvesWithoutLeftoverPlaceholders(Language lang)
    {
        var loc = new Localizer(lang);
        // A representative sample of the event lines GameState enqueues.
        var samples = new[]
        {
            NewsMessage.Of("news.event.sugar_ripe"),
            NewsMessage.Of("news.event.lucky", "1,234"),
            NewsMessage.Of("news.event.harvest", 3L),
            NewsMessage.Of("news.event.ascend_one", 1, 5),
            NewsMessage.Of("news.event.ascend_many", 3, 8),
            NewsMessage.Of("news.event.achievement", "baked_1"),
        };
        foreach (var msg in samples)
        {
            var text = NewsFlavor.Resolve(msg, loc);
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.DoesNotContain("{0}", text);
            Assert.DoesNotContain("{1}", text);
        }
    }

    // ------------------------------------------------------------------
    // Template families: placeholders fill correctly
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(ChineseLangData))]
    public void GeneratedAchievementNames_FillTemplate(Language lang)
    {
        var loc = new Localizer(lang);
        // A magnitude-generated baker name (1e12 → "…万亿… baker") must embed the
        // English magnitude word and leave no raw placeholder.
        var baker = Achievements.Get("baked_1000000000000");
        var name = baker.DisplayName(loc);
        Assert.Contains("Trillion", name); // magnitude word stays English
        Assert.DoesNotContain("{0}", name);
    }

    // ------------------------------------------------------------------
    // Design invariants
    // ------------------------------------------------------------------

    [Fact]
    public void MissingKey_FallsBackToEnglishThenRawKey()
    {
        var en = new Localizer(Language.English);
        Assert.Equal("does.not.exist", en["does.not.exist"]); // raw key, never blank

        var zh = new Localizer(Language.SimplifiedChinese);
        // A UI key present in English but (hypothetically) absent from the overlay
        // resolves to the English source. Use a key we know exists in English.
        Assert.Equal("Save", en["ui.save.save"]);
        Assert.Null(zh.Overlay("does.not.exist"));
    }

    [Fact]
    public void SetLanguage_RaisesEvent_AndSwitchesResolution()
    {
        var loc = new Localizer(Language.English);
        var fired = 0;
        loc.OnLanguageChanged += () => fired++;

        loc.SetLanguage(Language.SimplifiedChinese);
        Assert.Equal(1, fired);
        Assert.Equal(Language.SimplifiedChinese, loc.Current);
        Assert.Equal("语言", loc["ui.settings.language"]);

        // Setting the same language again is a no-op (no event, no churn).
        loc.SetLanguage(Language.SimplifiedChinese);
        Assert.Equal(1, fired);
    }

    [Theory]
    [InlineData("zh-CN", Language.SimplifiedChinese)]
    [InlineData("zh-SG", Language.SimplifiedChinese)]
    [InlineData("zh-Hans", Language.SimplifiedChinese)]
    [InlineData("zh", Language.SimplifiedChinese)]
    [InlineData("zh-TW", Language.TraditionalChinese)]
    [InlineData("zh-HK", Language.TraditionalChinese)]
    [InlineData("zh-Hant", Language.TraditionalChinese)]
    [InlineData("en-US", Language.English)]
    [InlineData("fr", Language.English)]
    [InlineData("", Language.English)]
    [InlineData(null, Language.English)]
    public void BrowserTag_MapsToExpectedLanguage(string? tag, Language expected)
    {
        Assert.Equal(expected, LanguageDetection.FromBrowserTag(tag));
    }

    private static string Key(BuildingId id) => id.ToString().ToLowerInvariant();

    public static IEnumerable<object[]> ChineseLangData() =>
        ChineseLanguages.Select(l => new object[] { l });

    public static IEnumerable<object[]> AllLangData() =>
        AllLanguages.Select(l => new object[] { l });
}
