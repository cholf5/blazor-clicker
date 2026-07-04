using Game.Core.Data;

namespace Game.Core.Tests;

public class AchievementCategoriesTests
{
    [Fact]
    public void EveryAchievement_MapsToExactlyOneCategory()
    {
        foreach (var ach in Achievements.All)
        {
            var matches = AchievementCategories.All.Count(c => c.Matches(ach.Id));
            Assert.True(matches == 1,
                $"Achievement '{ach.Id}' matched {matches} categories (expected exactly 1).");
        }
    }

    [Fact]
    public void CategoryKeys_AreUnique()
    {
        var keys = AchievementCategories.All.Select(c => c.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void CategoryCounts_SumToTotalAchievementCount()
    {
        var categorized = AchievementCategories.All
            .Sum(c => Achievements.All.Count(a => c.Matches(a.Id)));
        Assert.Equal(Achievements.All.Count, categorized);
    }
}
