namespace Game.Core.Domain;

/// <summary>
/// Every kind of building the player can own. The order here matches the
/// order they appear in the shop UI (cheapest → most expensive).
/// </summary>
public enum BuildingId
{
    Cursor,
    Grandma,
    Farm,
    Mine,
    Factory,
    Bank,
    Temple,
    WizardTower,
    Shipment,
    AlchemyLab,
    Portal,
    TimeMachine,
    // ---- Late-game additions (see ADR 0002 §Extended catalog) ----
    AntimatterCondenser,
    Prism,
    Chancemaker,
    FractalEngine,
    JavascriptConsole,
    Idleverse,
}
