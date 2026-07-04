using Microsoft.JSInterop;

namespace Game.Web.Services;

/// <summary>
/// Facade over the JS-side <c>window.cookieClicker.play*</c> helpers defined
/// in <c>wwwroot/js/cookie-clicker.js</c>. Owns the persistent mute preference
/// and pushes it into the JS master gain whenever it flips.
///
/// All calls are fire-and-forget — a failed JS interop (e.g. browser blocked
/// audio because we haven't seen a user gesture yet) is swallowed rather
/// than propagated to the game logic.
/// </summary>
public sealed class AudioService
{
    private const string StorageKey = "cookie-clicker-remake.muted";

    private readonly IJSRuntime _js;
    private readonly LocalStorageService _storage;
    private bool _initialised;

    public bool IsMuted { get; private set; }

    public event Action? OnMuteChanged;

    public AudioService(IJSRuntime js, LocalStorageService storage)
    {
        _js = js;
        _storage = storage;
    }

    /// <summary>Load the persisted mute preference and push it to JS.</summary>
    public async Task InitAsync()
    {
        if (_initialised) return;
        _initialised = true;
        try
        {
            var raw = await _storage.GetItemAsync(StorageKey);
            IsMuted = raw == "1";
            await _js.InvokeVoidAsync("cookieClicker.setMuted", IsMuted);
        }
        catch
        {
            // Non-fatal — audio just stays at default until the user toggles.
        }
    }

    public async Task SetMutedAsync(bool muted)
    {
        IsMuted = muted;
        try { await _storage.SetItemAsync(StorageKey, muted ? "1" : "0"); } catch { }
        try { await _js.InvokeVoidAsync("cookieClicker.setMuted", muted); } catch { }
        OnMuteChanged?.Invoke();
    }

    public Task ToggleAsync() => SetMutedAsync(!IsMuted);

    public void PlayClick() => Fire("cookieClicker.playClick");
    public void PlayPurchase() => Fire("cookieClicker.playPurchase");
    public void PlayGolden() => Fire("cookieClicker.playGolden");
    public void PlayAchievement() => Fire("cookieClicker.playAchievement");
    public void PlayAscend() => Fire("cookieClicker.playAscend");

    private void Fire(string method)
    {
        // Never await — sound effects must never block the game loop.
        _ = InvokeSafeAsync(method);
    }

    private async Task InvokeSafeAsync(string method)
    {
        try { await _js.InvokeVoidAsync(method); }
        catch { /* ignore transient JS interop errors */ }
    }
}
