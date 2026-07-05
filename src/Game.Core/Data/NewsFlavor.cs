using Game.Core.Domain;
using Game.Core.Localization;

namespace Game.Core.Data;

/// <summary>
/// Flavor text that scrolls in the news ticker, expressed as translation keys
/// rather than literal strings — the actual English wording lives in the
/// English translation dictionary (the single source), and the UI resolves a
/// key against the current language at display time. Two kinds of ambient entry:
///
/// * <see cref="IdleKeys"/> — ambient snippets shown at random between events.
/// * <see cref="ProgressMessages"/> — milestone lines tied to how many cookies
///   the player has ever baked, giving the ticker a sense of progression.
///
/// Actual "event" lines (achievements, golden cookies, sugar lumps, ascensions)
/// are enqueued by <see cref="GameState"/> as <see cref="NewsMessage"/>s and
/// take priority over the ambient pool.
/// </summary>
public static class NewsFlavor
{
    /// <summary>Keys for the random ambient headlines. Wording lives in the translation dictionaries.</summary>
    public static readonly IReadOnlyList<string> IdleKeys =
    [
        "news.idle.delicious",
        "news.idle.grandma_award",
        "news.idle.portal_stable",
        "news.idle.wizard_union",
        "news.idle.bumper_crop",
        "news.idle.cursor_billionth",
        "news.idle.time_machine_repair",
        "news.idle.alchemy_art",
        "news.idle.antimatter_safety",
        "news.idle.prism_innovation",
        "news.idle.chancemaker_nat20",
        "news.idle.fractal_storage",
        "news.idle.idleverse_variance",
        "news.idle.js_undefined",
        "news.idle.bank_crumbs",
        "news.idle.shipment_blackholes",
        "news.idle.clicking_editorial",
        "news.idle.year_of_cookie",
        "news.idle.chocolate_futures",
        "news.idle.headlines_taste",
    ];

    /// <summary>
    /// Progressive headline keys gated on lifetime bake count. The picker walks
    /// the list in order and returns the last key whose threshold has been
    /// crossed.
    /// </summary>
    public static readonly IReadOnlyList<(double Threshold, string Key)> ProgressMessages =
    [
        (0,                 "news.progress.welcome"),
        (100,               "news.progress.first_hundred"),
        (10_000,            "news.progress.bake_off"),
        (1_000_000,         "news.progress.millionaire"),
        (1_000_000_000,     "news.progress.industries"),
        (1_000_000_000_000, "news.progress.trillion"),
        (1e15,              "news.progress.interdimensional"),
        (1e18,              "news.progress.cosmic_scale"),
        (1e21,              "news.progress.universe_cookie"),
    ];

    /// <summary>Pick an ambient message key deterministically for the given seed.</summary>
    public static string PickIdleKey(Random rng) => IdleKeys[rng.Next(IdleKeys.Count)];

    /// <summary>Find the newest progression message key the player has unlocked.</summary>
    public static string? PickProgressKey(GameState state)
    {
        string? current = null;
        foreach (var (threshold, key) in ProgressMessages)
        {
            if (state.AllTimeCookiesBaked >= threshold) current = key;
            else break;
        }
        return current;
    }

    /// <summary>
    /// Resolve a queued <see cref="NewsMessage"/> to a localized display string.
    /// The achievement-unlock line carries an achievement id in its args, which
    /// is expanded to that achievement's localized name here so the ticker text
    /// tracks the current language.
    /// </summary>
    public static string Resolve(NewsMessage msg, ILocalizer loc)
    {
        var args = msg.Args ?? Array.Empty<object>();
        if (msg.Key == "news.event.achievement" && args.Length == 1 && args[0] is string achId)
        {
            // Resolve the achievement's localized name; guard against an unknown
            // id so a bad enqueue can never crash the ticker.
            var name = Achievements.TryGet(achId, out var def) ? def!.DisplayName(loc) : achId;
            return loc.Format(msg.Key, name);
        }
        return args.Length == 0 ? loc[msg.Key] : loc.Format(msg.Key, args);
    }
}
