using Game.Core.Domain;

namespace Game.Core.Data;

/// <summary>
/// Flavor text that scrolls in the news ticker. Two kinds of entries:
///
/// * <see cref="Idle"/> — ambient snippets shown at random between events.
/// * <see cref="ProgressMessages"/> — milestone lines tied to how many
///   cookies the player has ever baked, giving the ticker some feel of
///   progression.
///
/// Actual "event" lines (achievements, golden cookies, sugar lumps,
/// ascensions) are enqueued directly by <see cref="GameState"/> and take
/// priority over the ambient pool.
/// </summary>
public static class NewsFlavor
{
    /// <summary>Random ambient news headlines. Original writing.</summary>
    public static readonly IReadOnlyList<string> Idle =
    [
        "Study finds cookies remain scientifically delicious.",
        "Local grandma nominated for prestigious baking award — again.",
        "Portal to cookie dimension declared 'entirely stable, don't worry about it'.",
        "Wizard tower unionises; demands better working conditions and fewer eldritch summonings.",
        "Farmers report bumper crop of chocolate chips this season.",
        "Cursor factory produces one billionth pointer, celebrates modestly.",
        "Time machine repair shop backed up until last Tuesday.",
        "Alchemists insist that turning cookies into gold is 'more of an art than a science'.",
        "Antimatter condenser meets safety inspection with only minor universe-warping.",
        "Prism refracts sunbeam directly into oven; bakery hailed as innovation hub.",
        "Chancemaker rolls a nat 20 on 'bake a really good batch'.",
        "Fractal engine outputs infinite cookies; storage remains the bottleneck.",
        "Idleverse council reports 'no notable variance' in cookie output across realities.",
        "Javascript console: `undefined is not a cookie` — engineers reassured that everything is fine.",
        "Bank vault repurposed for cookie storage; interest paid in crumbs.",
        "Shipment routes now avoid known black holes on request.",
        "Editorial: are we clicking too much? A dietician weighs in.",
        "Local news declares 2026 'the year of the cookie'. Again.",
        "Chocolate futures spike after unusually productive Tuesday.",
        "Studies suggest reading news headlines improves the taste of cookies. Findings unconfirmed.",
    ];

    /// <summary>
    /// Progressive headlines gated on lifetime bake count. The picker walks
    /// the list in order and shows the first message whose threshold has
    /// been crossed but not yet crossed the next one.
    /// </summary>
    public static readonly IReadOnlyList<(double Threshold, string Message)> ProgressMessages =
    [
        (0,                     "Welcome to your bakery. Click the cookie to get started."),
        (100,                   "Your first hundred cookies are baked. The kitchen smells great."),
        (10_000,                "News: local bakery wins bake-off with entry titled 'more'."),
        (1_000_000,             "You are now officially a cookie millionaire. Consider hiring an accountant."),
        (1_000_000_000,         "Cookie output has surpassed several small industries. Regulators inquire."),
        (1_000_000_000_000,     "You have baked a trillion cookies. A trillion. Read that again."),
        (1e15,                  "Interdimensional bakers request a friendly rivalry match."),
        (1e18,                  "Astronomers redefine 'cosmic scale' after seeing your bank."),
        (1e21,                  "Physicists concede that the universe may in fact be a cookie."),
    ];

    /// <summary>Pick an ambient message deterministically for the given seed.</summary>
    public static string PickIdle(Random rng) => Idle[rng.Next(Idle.Count)];

    /// <summary>Find the newest progression message the player has unlocked.</summary>
    public static string? PickProgress(GameState state)
    {
        string? current = null;
        foreach (var (threshold, msg) in ProgressMessages)
        {
            if (state.AllTimeCookiesBaked >= threshold) current = msg;
            else break;
        }
        return current;
    }
}
