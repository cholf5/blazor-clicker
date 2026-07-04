using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Core.Domain;

namespace Game.Core;

/// <summary>
/// Serializes and deserializes <see cref="GameState"/> to/from a compact
/// JSON string. Handles version migrations (currently a no-op since we're
/// on version 1).
///
/// Exported saves are additionally Base64-wrapped so they're safe to share
/// through chat / email without JSON needing escaping.
/// </summary>
public static class SaveSystem
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Convert <paramref name="state"/> to a compact JSON string.</summary>
    public static string SerializeToJson(GameState state) =>
        JsonSerializer.Serialize(state.ToSaveData(), Options);

    /// <summary>Deserialize a save into a fresh <see cref="GameState"/>.</summary>
    public static GameState DeserializeFromJson(string json)
    {
        var data = JsonSerializer.Deserialize<SaveData>(json, Options)
                   ?? throw new InvalidDataException("Save data was null or malformed.");

        Migrate(data);

        var state = new GameState();
        state.ApplyLoaded(data);
        return state;
    }

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

        // Future: while (data.Version < CurrentVersion) { migrate; data.Version++; }
        data.Version = CurrentVersion;
    }
}
