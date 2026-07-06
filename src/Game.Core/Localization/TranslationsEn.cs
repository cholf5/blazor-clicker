namespace Game.Core.Localization;

/// <summary>
/// English translation dictionary — the <b>source</b> language. It defines
/// every key the game resolves through the indexer/<c>Format</c> (UI chrome and
/// news lines) so a key absent from a Chinese overlay always falls back to a
/// real English string.
///
/// It deliberately does <b>not</b> contain building / upgrade / achievement
/// names and descriptions: those keep their English wording inline in the
/// catalogs (<see cref="Data.Buildings"/>, <see cref="Data.Upgrades"/>,
/// <see cref="Data.Achievements"/>) and are only overridden by the Chinese
/// overlays, so there is exactly one English source for each string.
/// </summary>
public static class TranslationsEn
{
    public static readonly IReadOnlyDictionary<string, string> Entries = new Dictionary<string, string>
    {
        // ---- Options menu (modal shell + section headings) ------------------
        ["ui.options.open"] = "Options",
        ["ui.options.title"] = "Options",
        ["ui.options.close"] = "Close",
        ["ui.options.section_data"] = "Save data",
        ["ui.options.section_sound"] = "Sound",
        ["ui.options.section_language"] = "Language",

        // ---- Language selector (shown in each language's own script) --------
        ["ui.settings.language"] = "Language",
        ["ui.language.system"] = "System default",
        ["ui.language.english"] = "English",
        ["ui.language.zh_hans"] = "简体中文",
        ["ui.language.zh_hant"] = "繁體中文",

        // ---- Stats panel ----------------------------------------------------
        ["ui.stats.cookies_unit"] = "cookies",
        ["ui.stats.per_second"] = "per second:",
        ["ui.stats.baked"] = "baked: {0}",
        ["ui.stats.clicks"] = "clicks: {0}",
        ["ui.stats.baked_title"] = "Total cookies ever baked",
        ["ui.stats.clicks_title"] = "Manual clicks so far",
        ["ui.stats.ach_title"] = "Achievements unlocked",
        ["ui.buff.frenzy"] = "Frenzy ×7 CPS",
        ["ui.buff.click_frenzy"] = "Click frenzy ×777",

        // ---- Big cookie -----------------------------------------------------
        ["ui.cookie.aria"] = "Bake a cookie",
        ["ui.cookie.title"] = "Click to bake a cookie",
        ["ui.golden.aria"] = "Golden cookie!",

        // ---- Buildings shop -------------------------------------------------
        ["ui.buildings.title"] = "Buildings",
        ["ui.building.locked_name"] = "???",
        ["ui.building.unlocks_at"] = "Unlocks at {0} baked",
        ["ui.building.owns"] = "owns {0}",
        ["ui.building.cps_total"] = "— {0} cps",
        ["ui.building.cps_each"] = "— {0} cps each",
        ["ui.building.locked_flavor"] = "Keep baking to reveal the next building.",
        ["ui.building.owned"] = "Owned",
        ["ui.building.each_produces"] = "Each produces",
        ["ui.building.total_from"] = "Total from these",
        ["ui.building.share"] = "Share of your cps",
        ["ui.building.pays_for"] = "Pays for itself in",
        ["ui.building.sugar_level"] = "Sugar level",
        ["ui.building.sugar_level_value"] = "Lv {0} (+{1}%)",
        ["ui.unit.cps"] = "{0} cps",

        // ---- Upgrade store --------------------------------------------------
        ["ui.upgrades.title"] = "Upgrades",
        ["ui.upgrades.count"] = "{0} available",
        ["ui.upgrades.empty"] = "Keep clicking and buying buildings to unlock upgrades.",
        ["ui.tooltip.effect"] = "Effect",
        ["ui.effect.building_multiplier"] = "×{0} {1} output",
        ["ui.effect.click_multiplier"] = "×{0} click power",
        ["ui.effect.global_multiplier"] = "×{0} all production",
        ["ui.effect.kitten"] = "×{0} all production (+{1}% per 100% milk)",
        ["ui.effect.cursor_synergy"] = "+{0} cursor cps per non-cursor building",
        ["ui.effect.building_fallback"] = "building",
        ["ui.upgrade_category.building"] = "Building",
        ["ui.upgrade_category.cursor"] = "Cursor",
        ["ui.upgrade_category.clicking"] = "Clicking",
        ["ui.upgrade_category.kitten"] = "Kitten",

        // ---- Milk (decorative pool + tooltip) -------------------------------
        ["ui.milk.title"] = "Milk",
        ["ui.milk.level"] = "Milk level",
        ["ui.milk.effect"] = "Boost to production",
        ["ui.milk.flavor"] = "Milk rises as you unlock achievements. On its own it does nothing — your kitten upgrades are what convert it into extra production.",
        ["ui.milk.flavor_none"] = "Milk rises as you unlock achievements. By itself it produces nothing: the boost stays ×1 until you buy a kitten upgrade, which turns your milk into extra cookies. Look for one in the Upgrades panel.",

        // ---- Cursor ring (decorative orbiting fingers + tooltip) ------------
        ["ui.cursorring.title"] = "Cursors",
        ["ui.cursorring.owned"] = "Cursors owned",
        ["ui.cursorring.tier"] = "Ring colour",
        ["ui.cursorring.per_finger"] = "Cursors per finger",
        ["ui.cursorring.next_merge"] = "Cursors to next merge",
        ["ui.cursorring.tier.0"] = "White",
        ["ui.cursorring.tier.1"] = "Green",
        ["ui.cursorring.tier.2"] = "Blue",
        ["ui.cursorring.tier.3"] = "Purple",
        ["ui.cursorring.tier.4"] = "Orange",
        ["ui.cursorring.tier.5"] = "Red",
        ["ui.cursorring.tier.6"] = "Gold",
        ["ui.cursorring.flavor"] = "Every cursor taps the cookie for you, orbiting it as it works. Once the rings fill up, each pair merges into a brighter finger worth twice as many.",
        ["ui.cursorring.flavor_max"] = "The rings burn gold and can grow no further — every finger is worth its maximum. Your cursors are, frankly, showing off now.",

        // ---- Achievements panel ---------------------------------------------
        ["ui.achievements.title"] = "Achievements",
        ["ui.achievements.locked_title"] = "???",
        ["ui.achievements.locked_flavor"] = "This achievement is still locked.",
        ["ui.achievements.unlocked_tag"] = "Unlocked",
        ["ui.achievements.toast_title"] = "Achievement unlocked!",

        // ---- Achievement category labels ------------------------------------
        ["category.baking.label"] = "Baking",
        ["category.buildings.label"] = "Buildings",
        ["category.production.label"] = "Production",
        ["category.clicking.label"] = "Clicking",
        ["category.golden.label"] = "Golden cookies",
        ["category.upgrades.label"] = "Upgrades",
        ["category.sugar.label"] = "Sugar lumps",
        ["category.prestige.label"] = "Prestige",
        ["category.dedication.label"] = "Dedication",

        // ---- Sugar lump widget ----------------------------------------------
        ["ui.sugar.title"] = "Sugar lumps",
        ["ui.sugar.harvest"] = "Harvest ripe lump",
        ["ui.sugar.harvest_title"] = "Harvest this sugar lump",
        ["ui.sugar.growing"] = "Growing… {0}",
        ["ui.sugar.blurb"] = "Spend lumps to level up buildings: +{0}% output each, for that building.",
        ["ui.sugar.next_tooltip"] = "Next lump ripens in {0}. Spend lumps to level up buildings.",
        ["ui.sugar.spend"] = "Spend on buildings",
        ["ui.sugar.spend_title"] = "Invest sugar lumps into building levels",
        ["ui.sugar.modal_title"] = "Spend sugar lumps",
        ["ui.sugar.modal_hint"] = "Each level adds +{0}% to that building's output. Reaching level N costs N lumps.",
        ["ui.sugar.balance"] = "{0} available",
        ["ui.sugar.level"] = "Lv {0}",
        ["ui.sugar.levelup"] = "Level up ({0} 🍬)",
        ["ui.sugar.levelup_title"] = "Spend {0} sugar lumps to add +1% to this building",
        ["ui.sugar.tip_balance"] = "Unspent",
        ["ui.sugar.tip_ripen"] = "Ripens every",
        ["ui.sugar.tip_ripen_value"] = "~{0} h (one at a time)",
        ["ui.sugar.tip_effect"] = "Each level",
        ["ui.sugar.tip_effect_value"] = "+{0}% to one building",
        ["ui.sugar.tip_how"] = "A slow-growing currency. Harvest ripe lumps, then click \"Spend on buildings\" to invest them: each level permanently boosts that one building's output.",

        // ---- Ascend panel ---------------------------------------------------
        ["ui.ascend.title"] = "Ascension",
        ["ui.ascend.current_title"] = "Permanent CPS bonus from prior ascensions",
        ["ui.ascend.confirm_body"] = "Ascending will wipe the current run — cookies, buildings, and upgrades reset. Achievements, sugar lumps and prestige stay with you.",
        ["ui.ascend.confirm_gain"] = "You will gain +{0} prestige levels.",
        ["ui.ascend.confirm_gain_one"] = "You will gain +{0} prestige level.",
        ["ui.ascend.button"] = "Ascend",
        ["ui.ascend.cancel"] = "Cancel",
        ["ui.ascend.blurb"] = "Ascend now to gain +{0} prestige levels — each level grants a permanent +{1}% CPS boost.",
        ["ui.ascend.blurb_one"] = "Ascend now to gain +{0} prestige level — each level grants a permanent +{1}% CPS boost.",
        ["ui.ascend.locked"] = "Reach {0} cookies baked this run to unlock your first ascension. Currently: {1}.",

        // ---- Save menu ------------------------------------------------------
        ["ui.save.save"] = "Save",
        ["ui.save.save_title"] = "Save now",
        ["ui.save.export"] = "Export",
        ["ui.save.export_title"] = "Copy save to clipboard",
        ["ui.save.export_file"] = "Export to file",
        ["ui.save.import"] = "Import",
        ["ui.save.import_title"] = "Paste an exported save",
        ["ui.save.wipe"] = "Wipe",
        ["ui.save.wipe_title"] = "Delete save and start over",
        ["ui.save.import_placeholder"] = "Paste an exported save…",
        ["ui.save.load"] = "Load",
        ["ui.save.copy"] = "Copy",
        ["ui.save.or_prefix"] = "Or",
        ["ui.save.cancel"] = "Cancel",
        ["ui.save.saved"] = "Saved.",
        ["ui.save.exported"] = "Exported save copied to clipboard.",
        ["ui.save.export_manual"] = "Export ready — paste it somewhere safe.",
        ["ui.save.exported_file"] = "Save exported to file.",
        ["ui.save.import_file"] = "Or load from a file:",
        ["ui.save.loaded"] = "Save loaded.",
        ["ui.save.import_error"] = "Couldn't load save: {0}",
        ["ui.save.wipe_confirm"] = "Really wipe your save and start over?",
        ["ui.save.wiped"] = "Save wiped.",
        ["ui.mute.muted"] = "Sound is muted — click to unmute",
        ["ui.mute.on"] = "Sound is on — click to mute",

        // ---- Offline dialog -------------------------------------------------
        ["ui.offline.title"] = "🎉 Welcome back!",
        ["ui.offline.elapsed"] = "You were away for {0}.",
        ["ui.offline.earned"] = "Your bakery kept working at {0}% efficiency and produced:",
        ["ui.offline.lump"] = "A sugar lump ripened while you were away — go harvest it!",
        ["ui.offline.continue"] = "Continue baking",

        // ---- Footer ---------------------------------------------------------
        ["ui.footer.title"] = "Cookie Clicker · Blazor fan remake",
        ["ui.footer.subtitle"] = "Unofficial. Not affiliated with Orteil / DashNet. Autosaves every 15s to your browser's local storage.",

        // ---- News ticker: ambient idle lines --------------------------------
        ["news.idle.delicious"] = "Study finds cookies remain scientifically delicious.",
        ["news.idle.grandma_award"] = "Local grandma nominated for prestigious baking award — again.",
        ["news.idle.portal_stable"] = "Portal to cookie dimension declared 'entirely stable, don't worry about it'.",
        ["news.idle.wizard_union"] = "Wizard tower unionises; demands better working conditions and fewer eldritch summonings.",
        ["news.idle.bumper_crop"] = "Farmers report bumper crop of chocolate chips this season.",
        ["news.idle.cursor_billionth"] = "Cursor factory produces one billionth pointer, celebrates modestly.",
        ["news.idle.time_machine_repair"] = "Time machine repair shop backed up until last Tuesday.",
        ["news.idle.alchemy_art"] = "Alchemists insist that turning cookies into gold is 'more of an art than a science'.",
        ["news.idle.antimatter_safety"] = "Antimatter condenser meets safety inspection with only minor universe-warping.",
        ["news.idle.prism_innovation"] = "Prism refracts sunbeam directly into oven; bakery hailed as innovation hub.",
        ["news.idle.chancemaker_nat20"] = "Chancemaker rolls a nat 20 on 'bake a really good batch'.",
        ["news.idle.fractal_storage"] = "Fractal engine outputs infinite cookies; storage remains the bottleneck.",
        ["news.idle.idleverse_variance"] = "Idleverse council reports 'no notable variance' in cookie output across realities.",
        ["news.idle.js_undefined"] = "Javascript console: `undefined is not a cookie` — engineers reassured that everything is fine.",
        ["news.idle.bank_crumbs"] = "Bank vault repurposed for cookie storage; interest paid in crumbs.",
        ["news.idle.shipment_blackholes"] = "Shipment routes now avoid known black holes on request.",
        ["news.idle.clicking_editorial"] = "Editorial: are we clicking too much? A dietician weighs in.",
        ["news.idle.year_of_cookie"] = "Local news declares 2026 'the year of the cookie'. Again.",
        ["news.idle.chocolate_futures"] = "Chocolate futures spike after unusually productive Tuesday.",
        ["news.idle.headlines_taste"] = "Studies suggest reading news headlines improves the taste of cookies. Findings unconfirmed.",

        // ---- News ticker: progression lines ---------------------------------
        ["news.progress.welcome"] = "Welcome to your bakery. Click the cookie to get started.",
        ["news.progress.first_hundred"] = "Your first hundred cookies are baked. The kitchen smells great.",
        ["news.progress.bake_off"] = "News: local bakery wins bake-off with entry titled 'more'.",
        ["news.progress.millionaire"] = "You are now officially a cookie millionaire. Consider hiring an accountant.",
        ["news.progress.industries"] = "Cookie output has surpassed several small industries. Regulators inquire.",
        ["news.progress.trillion"] = "You have baked a trillion cookies. A trillion. Read that again.",
        ["news.progress.interdimensional"] = "Interdimensional bakers request a friendly rivalry match.",
        ["news.progress.cosmic_scale"] = "Astronomers redefine 'cosmic scale' after seeing your bank.",
        ["news.progress.universe_cookie"] = "Physicists concede that the universe may in fact be a cookie.",

        // ---- News ticker: event lines (enqueued by GameState) ---------------
        ["news.event.sugar_ripe"] = "A sugar lump is ripe — go harvest it!",
        ["news.event.lucky"] = "Lucky! +{0} cookies.",
        ["news.event.frenzy"] = "Frenzy! CPS ×7 for 77 seconds.",
        ["news.event.click_frenzy"] = "Click frenzy! Click power ×777 for 13 seconds.",
        ["news.event.harvest"] = "Harvested a sugar lump! You now have {0}.",
        ["news.event.ascend_one"] = "Ascended! Gained {0} prestige level. New total: {1}.",
        ["news.event.ascend_many"] = "Ascended! Gained {0} prestige levels. New total: {1}.",
        ["news.event.golden_spawn"] = "A golden cookie is glinting somewhere on screen…",
        ["news.event.achievement"] = "Achievement unlocked: {0}!",
    };
}
