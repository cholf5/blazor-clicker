using System.Timers;

namespace Game.Web.Services;

/// <summary>
/// Drives the game clock. Ticks ~30 times per second and invokes
/// <see cref="OnTick"/> so subscribers can request a re-render.
///
/// Reads the current <see cref="GameState"/> from <see cref="SaveCoordinator"/>
/// on each tick rather than caching it, so state replacement (import / wipe)
/// takes effect immediately.
/// </summary>
public sealed class GameLoop : IDisposable
{
    private readonly System.Timers.Timer _timer;
    private readonly SaveCoordinator _save;
    private DateTime _lastTick = DateTime.UtcNow;

    public event Action? OnTick;

    public GameLoop(SaveCoordinator save)
    {
        _save = save;
        _timer = new System.Timers.Timer(1000.0 / 30.0);
        _timer.Elapsed += HandleTick;
        _timer.AutoReset = true;
    }

    public void Start()
    {
        _lastTick = DateTime.UtcNow;
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private async void HandleTick(object? sender, ElapsedEventArgs e)
    {
        var now = DateTime.UtcNow;
        var delta = (now - _lastTick).TotalSeconds;
        _lastTick = now;
        if (delta > 5) delta = 5;

        _save.State.Tick(delta);

        // Autosave throttling lives inside SaveCoordinator.
        try { await _save.MaybeAutosaveAsync(); } catch { /* ignore transient JS interop errors */ }

        OnTick?.Invoke();
    }

    public void Dispose()
    {
        _timer.Elapsed -= HandleTick;
        _timer.Dispose();
    }
}
