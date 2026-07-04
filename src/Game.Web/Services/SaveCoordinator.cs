using Game.Core;
using Game.Core.Domain;

namespace Game.Web.Services;

/// <summary>
/// Coordinates autosave. Owns the current <see cref="GameState"/> instance,
/// loads it on app startup, and periodically writes it back to localStorage.
///
/// On load, also detects wall-clock time elapsed since the last save was
/// written and asks <see cref="GameState.ApplyOfflineProgress"/> to grant
/// reduced-efficiency offline earnings.
/// </summary>
public sealed class SaveCoordinator
{
    private const string StorageKey = "cookie-clicker-remake.save";
    private const double AutosaveIntervalSeconds = 15;

    private readonly LocalStorageService _storage;
    private DateTime _lastSavedAt = DateTime.MinValue;

    public GameState State { get; private set; } = new();

    /// <summary>
    /// If the most recent load produced offline earnings, this is populated
    /// so the UI can flash a "welcome back" dialog. Cleared by the UI once
    /// it has been shown.
    /// </summary>
    public OfflineEarningsSummary? PendingOfflineSummary { get; private set; }

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

            State = SaveSystem.DeserializeFromJson(raw, out var savedAtUnix);

            // If we know when the save was written, credit the player for the
            // time they were away. Anything less than the report threshold is
            // silently absorbed inside ApplyOfflineProgress.
            if (savedAtUnix > 0)
            {
                var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var elapsed = nowUnix - savedAtUnix;
                if (elapsed > 0)
                {
                    var summary = State.ApplyOfflineProgress(elapsed);
                    if (summary.CookiesEarned > 0)
                        PendingOfflineSummary = summary;
                }
            }

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
        PendingOfflineSummary = null;
        OnStateReplaced?.Invoke();
    }

    /// <summary>Replace the current state from an import string.</summary>
    public async Task ImportAsync(string blob)
    {
        var loaded = SaveSystem.ImportFromString(blob);
        State = loaded;
        PendingOfflineSummary = null;
        await SaveNowAsync();
        OnStateReplaced?.Invoke();
    }

    /// <summary>Produce a shareable export string.</summary>
    public string Export() => SaveSystem.ExportToString(State);

    /// <summary>Clear the pending offline summary after the UI has displayed it.</summary>
    public void AcknowledgeOfflineSummary() => PendingOfflineSummary = null;
}
