namespace Game.Core.Domain;

/// <summary>
/// A news-ticker line the domain wants to show, expressed as a translation
/// <see cref="Key"/> plus optional format <see cref="Args"/> rather than a
/// finished sentence. This keeps <see cref="GameState"/> free of any localizer
/// dependency: it enqueues keys, and the UI resolves them against the current
/// language when draining the queue — so switching language re-renders even
/// freshly-emitted lines correctly.
///
/// Args are restricted to language-independent values (already-formatted
/// numbers via <see cref="NumberFormat"/>, which keep English magnitude words
/// by design) so they need no further translation at display time.
/// </summary>
public readonly record struct NewsMessage(string Key, object[]? Args)
{
    public static NewsMessage Of(string key, params object[] args) =>
        new(key, args.Length == 0 ? null : args);
}
