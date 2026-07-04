using Game.Core;
using Game.Core.Domain;

namespace Game.Web.Services;

/// <summary>
/// Coordinates autosave. Owns the current <see cref="GameState"/> instance,
/// loads it on app startup, and periodically writes it back to localStorage.
/// </summary>
public sealed class SaveCoordinator
{
    private const string StorageKey = "cookie-clicker-remake.save";
    private const double AutosaveIntervalSeconds = 15;

    private readonly LocalStorageService _storage;
    private double _lastSavedGameTime;
    private DateTime _lastSavedAt = DateTime.MinValue;

    public GameState State { get; private set; } = new();

    /// <summary>Signal to the UI that the underlying state instance was replaced (e.g. after import).</summary>
    public event Action? OnStateReplaced;

    public SaveCoordinator(LocalStorageService storage)
    {
        _storage = storage;
    }

    /// <summary>Load an existing save from localStorage, or leave a fresh state in place.</summary>
    public async Task LoadAsync()
    {
        try
        {
            var raw = await _storage.GetItemAsync(StorageKey);
            if (string.IsNullOrWhiteSpace(raw)) return;
            State = SaveSystem.DeserializeFromJson(raw);
            OnStateReplaced?.Invoke();
        }
        catch
        {
            // Corrupt save — start fresh rather than blocking the player.
        }
    }

    /// <summary>Write the current state to localStorage immediately.</summary>
    public async Task SaveNowAsync()
    {
        var json = SaveSystem.SerializeToJson(State);
        await _storage.SetItemAsync(StorageKey, json);
        _lastSavedGameTime = State.GameTime;
        _lastSavedAt = DateTime.UtcNow;
    }

    /// <summary>Call every tick; saves only when the throttle window has elapsed.</summary>
    public async Task MaybeAutosaveAsync()
    {
        var elapsed = (DateTime.UtcNow - _lastSavedAt).TotalSeconds;
        if (elapsed >= AutosaveIntervalSeconds) await SaveNowAsync();
    }

    /// <summary>Erase the save and start over.</summary>
    public async Task WipeAsync()
    {
        await _storage.RemoveItemAsync(StorageKey);
        State = new GameState();
        OnStateReplaced?.Invoke();
    }

    /// <summary>Replace the current state from an import string.</summary>
    public async Task ImportAsync(string blob)
    {
        var loaded = SaveSystem.ImportFromString(blob);
        State = loaded;
        await SaveNowAsync();
        OnStateReplaced?.Invoke();
    }

    /// <summary>Produce a shareable export string.</summary>
    public string Export() => SaveSystem.ExportToString(State);
}
