using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Core.Domain;

namespace Game.Core;

/// <summary>
/// Serializes and deserializes <see cref="GameState"/> to/from a compact
/// JSON string. Handles version migrations from every prior schema up to
/// <see cref="CurrentVersion"/>.
///
/// Exported saves are additionally Base64-wrapped so they're safe to share
/// through chat / email without JSON needing escaping.
/// </summary>
public static class SaveSystem
{
    public const int CurrentVersion = 6;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Convert <paramref name="state"/> to a compact JSON string, stamped with the current wall-clock time.</summary>
    public static string SerializeToJson(GameState state)
    {
        var data = state.ToSaveData();
        data.SavedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return JsonSerializer.Serialize(data, Options);
    }

    /// <summary>Deserialize a save into a fresh <see cref="GameState"/>.</summary>
    public static GameState DeserializeFromJson(string json) =>
        DeserializeInternal(json, out _);

    /// <summary>
    /// Deserialize a save and additionally report the Unix timestamp (seconds)
    /// at which it was last written. Zero if the save pre-dates v2 or wasn't
    /// stamped for some reason.
    /// </summary>
    public static GameState DeserializeFromJson(string json, out long savedAtUnixSeconds) =>
        DeserializeInternal(json, out savedAtUnixSeconds);

    /// <summary>Produce a Base64-wrapped export string safe for sharing.</summary>
    public static string ExportToString(GameState state)
    {
        var json = SerializeToJson(state);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    /// <summary>Load a previously exported string. Accepts either raw JSON or Base64-wrapped.</summary>
    public static GameState ImportFromString(string blob)
    {
        var trimmed = (blob ?? "").Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new InvalidDataException("Save is empty.");

        // Raw JSON starts with {, otherwise assume Base64.
        var json = trimmed.StartsWith('{')
            ? trimmed
            : Encoding.UTF8.GetString(Convert.FromBase64String(trimmed));

        return DeserializeFromJson(json);
    }

    private static GameState DeserializeInternal(string json, out long savedAtUnixSeconds)
    {
        var data = JsonSerializer.Deserialize<SaveData>(json, Options)
                   ?? throw new InvalidDataException("Save data was null or malformed.");

        Migrate(data);
        savedAtUnixSeconds = data.SavedAtUnixSeconds;

        var state = new GameState();
        state.ApplyLoaded(data);
        return state;
    }

    /// <summary>
    /// Migrate an older save shape to the current one. Safe to extend by
    /// adding cases as versions accumulate.
    /// </summary>
    private static void Migrate(SaveData data)
    {
        if (data.Version > CurrentVersion)
            throw new InvalidDataException(
                $"Save file was produced by a newer version of the game (v{data.Version}). " +
                "Please update before loading it.");

        while (data.Version < CurrentVersion)
        {
            switch (data.Version)
            {
                case 1:
                    // v1 → v2: seed the lifetime baked counter from the run baked
                    // counter, and leave sugar lump / prestige at their defaults.
                    if (data.AllTimeCookiesBaked <= 0)
                        data.AllTimeCookiesBaked = data.TotalCookiesBaked;
                    // Old saves have no offline stamp; leave SavedAtUnixSeconds = 0
                    // so SaveCoordinator can decide not to apply offline earnings.
                    data.Version = 2;
                    break;

                case 2:
                    // v2 → v3: new counters. No data to backfill — HandmadeCookies
                    // and GoldenClicksDuringFrenzy simply start at 0 for old saves.
                    data.Version = 3;
                    break;

                case 3:
                    // v3 → v4: added the chosen-language field. Nothing to backfill —
                    // leaving Language null makes old saves follow the system language,
                    // which is exactly the "never chosen" behaviour we want.
                    data.Version = 4;
                    break;

                case 4:
                    // v4 → v5: sugar lumps became spendable (ADR 0006). The stored
                    // SugarLumps count changes meaning from "each = permanent +1%
                    // global" to "unspent balance". We keep the number as-is so the
                    // player loses nothing — they just re-invest it into buildings.
                    // BuildingLevels defaults to empty.
                    data.BuildingLevels ??= new();
                    data.Version = 5;
                    break;

                case 5:
                    // v5 → v6: added per-upgrade purchase timestamps so the Stats
                    // dialog can sort by recent purchase. We don't know when each
                    // pre-existing upgrade was actually bought, so backfill them
                    // all with the save's current GameTime. That clusters every
                    // legacy purchase at load time and lets subsequent buys sort
                    // above it — imprecise but monotonic, which is all "recent
                    // first" needs.
                    data.UpgradePurchaseTimes ??= new();
                    foreach (var id in data.PurchasedUpgrades)
                        data.UpgradePurchaseTimes.TryAdd(id, data.GameTime);
                    data.Version = 6;
                    break;

                default:
                    // Unknown intermediate version — refuse to guess.
                    throw new InvalidDataException(
                        $"Don't know how to migrate save from v{data.Version} to v{CurrentVersion}.");
            }
        }
    }
}
