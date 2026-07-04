namespace Game.Core;

/// <summary>
/// Pure, side-effect-free math used across the game economy. Kept together
/// so all economic decisions can be inspected in one place.
/// </summary>
public static class Formulas
{
    /// <summary>Rate at which the price of a building grows per copy owned.</summary>
    public const double PriceGrowth = 1.15;

    /// <summary>
    /// Cost of buying the (currentOwned+1)th copy of a building.
    /// </summary>
    public static double BuildingCost(double baseCost, int currentOwned) =>
        baseCost * Math.Pow(PriceGrowth, currentOwned);

    /// <summary>
    /// Total cost of buying <paramref name="amount"/> more copies of a
    /// building when <paramref name="currentOwned"/> are already owned.
    /// Uses the closed-form geometric sum for O(1) evaluation.
    /// </summary>
    public static double BulkBuildingCost(double baseCost, int currentOwned, int amount)
    {
        if (amount <= 0) return 0;
        // sum_{k=0..amount-1} baseCost * r^(currentOwned + k)
        // = baseCost * r^currentOwned * (r^amount - 1) / (r - 1)
        var rPow = Math.Pow(PriceGrowth, currentOwned);
        return baseCost * rPow * (Math.Pow(PriceGrowth, amount) - 1) / (PriceGrowth - 1);
    }

    /// <summary>Round up to a "friendly" display precision — 3 significant digits after the decimal.</summary>
    public static double RoundForDisplay(double value) =>
        double.IsFinite(value) ? Math.Round(value, 3, MidpointRounding.AwayFromZero) : value;
}
