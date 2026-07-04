using Game.Core;

namespace Game.Core.Tests;

public class FormulasTests
{
    [Fact]
    public void BuildingCost_FirstBuilding_EqualsBaseCost()
    {
        Assert.Equal(15, Formulas.BuildingCost(15, 0), 6);
    }

    [Fact]
    public void BuildingCost_GrowsBy_1_15_Each_Time()
    {
        var one = Formulas.BuildingCost(100, 1);
        var two = Formulas.BuildingCost(100, 2);
        Assert.Equal(1.15, two / one, 3);
    }

    [Fact]
    public void BulkBuildingCost_Matches_SumOfIndividualCosts()
    {
        var individual = 0.0;
        for (var i = 0; i < 10; i++) individual += Formulas.BuildingCost(100, i);
        var bulk = Formulas.BulkBuildingCost(100, 0, 10);
        Assert.Equal(individual, bulk, 4);
    }

    [Fact]
    public void BulkBuildingCost_ZeroAmount_IsZero()
    {
        Assert.Equal(0, Formulas.BulkBuildingCost(100, 5, 0));
    }
}
