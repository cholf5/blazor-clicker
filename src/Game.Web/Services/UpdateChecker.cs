using System.Net.Http.Json;
using System.Timers;

namespace Game.Web.Services;

/// <summary>
/// Detects when a newer build of the app has been deployed while the player is
/// still running an old one.
///
/// Why this exists: this is a Blazor WASM SPA on GitHub Pages with no service
/// worker. Once <c>index.html</c> has loaded, a player who never refreshes (a
/// common case for an idle game left open for hours) will never re-request it,
/// and so will stay on the old build forever — the 10-minute HTML cache only
/// helps pages that get requested again. We close that gap by polling a tiny
/// <c>version.json</c> (written by CI with the commit SHA) and, on a change,
/// letting the UI offer a non-intrusive "refresh" prompt.
///
/// The refresh timing is deliberately left to the player: force-reloading an
/// idle game mid-session would be hostile.
/// </summary>
public sealed class UpdateChecker : IDisposable
{
    /// <summary>How often to re-check. Idle sessions last hours, so 10 minutes
    /// is timely without adding meaningful load (the payload is a few bytes).</summary>
    private const double PollIntervalSeconds = 600;

    private readonly HttpClient _http;
    private readonly System.Timers.Timer _timer;

    /// <summary>The version observed at startup. Everything is compared against
    /// this. Stays null when the first fetch fails (local dev with no
    /// version.json, or a network blip) — see <see cref="ApplyPolledVersion"/>.</summary>
    private string? _baseline;

    /// <summary>True once a newer version has been observed. Latches: we report
    /// once, stop polling, and never flip back.</summary>
    public bool UpdateAvailable { get; private set; }

    /// <summary>Raised exactly once, when an update is first detected.</summary>
    public event Action? OnUpdateAvailable;

    public UpdateChecker(HttpClient http)
    {
        _http = http;
        _timer = new System.Timers.Timer(PollIntervalSeconds * 1000);
        _timer.Elapsed += HandleTick;
        _timer.AutoReset = true;
    }

    /// <summary>Record the baseline version, then begin polling. Safe to call
    /// once on startup after the app has rendered.</summary>
    public async Task StartAsync()
    {
        _baseline = await FetchVersionAsync();
        _timer.Start();
    }

    private async void HandleTick(object? sender, ElapsedEventArgs e)
    {
        var polled = await FetchVersionAsync();
        ApplyPolledVersion(polled);
    }

    /// <summary>
    /// The pure state machine: decide whether a polled version means "update
    /// available". Kept separate from the HTTP fetch so it can be unit-tested
    /// without a network. Latches on first detection and stops the timer.
    /// </summary>
    internal void ApplyPolledVersion(string? polled)
    {
        if (UpdateAvailable) return;   // already latched — report only once
        if (_baseline is null) return; // no trustworthy baseline → never nag
        if (polled is null) return;    // transient fetch failure → try again next tick
        if (polled == _baseline) return;

        UpdateAvailable = true;
        _timer.Stop();
        OnUpdateAvailable?.Invoke();
    }

    /// <summary>Test seam: set the startup baseline without hitting the network.</summary>
    internal void SetBaselineForTest(string? baseline) => _baseline = baseline;

    private async Task<string?> FetchVersionAsync()
    {
        try
        {
            // Cache-bust with a timestamp query: version.json would otherwise be
            // served from the HTTP cache and we'd poll a stale value forever.
            var url = $"version.json?_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            var doc = await _http.GetFromJsonAsync<VersionDto>(url);
            return doc?.Version;
        }
        catch
        {
            // 404 (local dev), offline, or malformed JSON — treat as "unknown".
            return null;
        }
    }

    public void Dispose()
    {
        _timer.Elapsed -= HandleTick;
        _timer.Dispose();
    }

    private sealed record VersionDto(string Version);
}
