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

    /// <summary>
    /// Frame gaps larger than this (seconds) are treated as "the tab was
    /// backgrounded / the machine slept" and credited via
    /// <see cref="GameState.CatchUpProgress"/> rather than a normal tick. A few
    /// seconds is comfortably above a foreground frame (~0.03s) yet below any
    /// real absence, so a brief GC/scheduler hiccup still ticks normally.
    /// </summary>
    private const double CatchUpThresholdSeconds = 3.0;

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

        // A normal foreground frame is ~1/30s. A much larger gap means the tab
        // was backgrounded (browsers throttle background timers to ~1/s or less)
        // or the machine slept: the timer stopped firing and this single wake-up
        // carries all the missed wall-clock time. Feeding that straight into
        // Tick would stretch an expiring Frenzy across the whole window and spawn
        // a burst of golden cookies, so route it through the dedicated catch-up
        // path instead, which credits full CPS without those artifacts.
        if (delta > CatchUpThresholdSeconds)
        {
            _save.State.CatchUpProgress(delta);
        }
        else if (delta > 0)
        {
            _save.State.Tick(delta);
        }

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
