using Game.Web.Services;

namespace Game.Web.Tests;

/// <summary>
/// Guards the update-detection state machine in <see cref="UpdateChecker"/>.
/// The contract: never nag without a trustworthy baseline, report a genuine
/// version change exactly once, and shrug off transient fetch failures.
///
/// Only the pure decision (<see cref="UpdateChecker.ApplyPolledVersion"/>) is
/// exercised — the HTTP fetch and timer are the untestable-without-a-browser
/// edges and are deliberately kept thin around this core.
/// </summary>
public class UpdateCheckerTests
{
    private static UpdateChecker WithBaseline(string? baseline)
    {
        // HttpClient is unused by ApplyPolledVersion; a bare instance is fine.
        var checker = new UpdateChecker(new HttpClient());
        checker.SetBaselineForTest(baseline);
        return checker;
    }

    [Fact]
    public void ReportsUpdate_WhenPolledVersionDiffersFromBaseline()
    {
        var checker = WithBaseline("abc1234");
        var raised = 0;
        checker.OnUpdateAvailable += () => raised++;

        checker.ApplyPolledVersion("def5678");

        Assert.True(checker.UpdateAvailable);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void StaysQuiet_WhenPolledVersionMatchesBaseline()
    {
        var checker = WithBaseline("abc1234");
        var raised = 0;
        checker.OnUpdateAvailable += () => raised++;

        checker.ApplyPolledVersion("abc1234");

        Assert.False(checker.UpdateAvailable);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void NeverNags_WhenBaselineIsUnknown()
    {
        // Baseline null models local dev (no version.json) or a failed first
        // fetch — without a trustworthy anchor we must not prompt.
        var checker = WithBaseline(null);
        var raised = 0;
        checker.OnUpdateAvailable += () => raised++;

        checker.ApplyPolledVersion("anything");

        Assert.False(checker.UpdateAvailable);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void IgnoresTransientFetchFailure()
    {
        // A null poll (404 / offline / bad JSON) must not be mistaken for a
        // version change; the next successful poll can still fire.
        var checker = WithBaseline("abc1234");
        var raised = 0;
        checker.OnUpdateAvailable += () => raised++;

        checker.ApplyPolledVersion(null);
        Assert.False(checker.UpdateAvailable);

        checker.ApplyPolledVersion("def5678");
        Assert.True(checker.UpdateAvailable);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void ReportsOnlyOnce_EvenAcrossMultipleChanges()
    {
        var checker = WithBaseline("abc1234");
        var raised = 0;
        checker.OnUpdateAvailable += () => raised++;

        checker.ApplyPolledVersion("def5678");
        checker.ApplyPolledVersion("ghi9012"); // yet another deploy — still silent

        Assert.True(checker.UpdateAvailable);
        Assert.Equal(1, raised);
    }
}
