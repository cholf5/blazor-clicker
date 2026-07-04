using Game.Core.Domain;

namespace Game.Core.Data;

/// <summary>
/// Static catalog of every building type in the game. Building names and
/// numeric constants (base cost, base CPS) mirror the well-known idle-baking
/// genre archetypes — see NOTICE.md for attribution. All flavor text in this
/// file is original wording written for this remake.
/// </summary>
public static class Buildings
{
    public static readonly IReadOnlyList<BuildingDefinition> All =
    [
        new(BuildingId.Cursor,             "Cursor",             "👆",  15,                 0.1,        "A tireless pointer that taps the cookie for you."),
        new(BuildingId.Grandma,            "Grandma",            "👵",  100,                1,          "Bakes fresh batches with decades of experience."),
        new(BuildingId.Farm,               "Farm",               "🌾",  1_100,              8,          "Cultivates fields where cookie crops sprout."),
        new(BuildingId.Mine,               "Mine",               "⛏️", 12_000,             47,         "Extracts raw dough and chocolate seams from deep rock."),
        new(BuildingId.Factory,            "Factory",            "🏭",  130_000,            260,        "An industrial line churning out cookies by the pallet."),
        new(BuildingId.Bank,               "Bank",               "🏦",  1_400_000,          1_400,      "Compound interest, but denominated in cookies."),
        new(BuildingId.Temple,             "Temple",             "⛩️", 20_000_000,         7_800,      "Ancient rites, faintly scented with cocoa."),
        new(BuildingId.WizardTower,        "Wizard tower",       "🧙", 330_000_000,        44_000,     "Conjures cookies out of thin air (and eldritch flour)."),
        new(BuildingId.Shipment,           "Shipment",           "🚀",  5_100_000_000,      260_000,    "Off-world convoys arriving fresh from the outer colonies."),
        new(BuildingId.AlchemyLab,         "Alchemy lab",        "⚗️", 75_000_000_000,     1_600_000,  "Transmutes rare metals into edible baked goods."),
        new(BuildingId.Portal,             "Portal",             "🌀",  1_000_000_000_000,  10_000_000, "A rift to a dimension where cookies fall like rain."),
        new(BuildingId.TimeMachine,        "Time machine",       "⏰",  14_000_000_000_000, 65_000_000, "Retrieves cookies from the instant before they are eaten."),
        // ---- Late-game tier ----
        new(BuildingId.AntimatterCondenser,"Antimatter condenser","🌌", 170_000_000_000_000d,       430_000_000d,     "Converts particle-antiparticle pairs into pure baked flavor."),
        new(BuildingId.Prism,              "Prism",              "🔺",  2_100_000_000_000_000d,     2_900_000_000d,   "Refracts starlight into chocolate-chip photons."),
        new(BuildingId.Chancemaker,        "Chancemaker",        "🎲",  26_000_000_000_000_000d,    21_000_000_000d,  "Warps probability so every roll bakes another cookie."),
        new(BuildingId.FractalEngine,      "Fractal engine",     "❄️", 310_000_000_000_000_000d,   150_000_000_000d, "Bakes cookies inside cookies inside cookies, ad infinitum."),
        new(BuildingId.JavascriptConsole,  "Javascript console", "💻",  71_000_000_000_000_000_000d, 1_100_000_000_000d, "console.log(new Cookie()) — surprisingly effective."),
        new(BuildingId.Idleverse,          "Idleverse",          "🪐",  12_000_000_000_000_000_000_000d, 8_300_000_000_000d, "Every parallel universe is also playing this game, and sharing the proceeds."),
    ];

    private static readonly Dictionary<BuildingId, BuildingDefinition> ById =
        All.ToDictionary(b => b.Id);

    public static BuildingDefinition Get(BuildingId id) => ById[id];
}
