using System;
using OwO_Maker.Core;

namespace OwO_Maker.Core.Tests;

/// <summary>
/// Deterministic, controllable TimeProvider for tests. Timestamps are expressed
/// in ticks (one tick = 100 ns) so advancing by a TimeSpan is exact.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private long _timestamp;

    public override long GetTimestamp() => _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public void Advance(TimeSpan by) => _timestamp += by.Ticks;
}

public class BotStatsTimingTests
{
    [Fact]
    public void Elapsed_BeforeStartRun_IsZero()
    {
        var time = new FakeTimeProvider();
        var stats = new BotStats(time);

        time.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.Zero, stats.Elapsed);
    }

    [Fact]
    public void Elapsed_AfterStartRun_AdvancesWithTime()
    {
        var time = new FakeTimeProvider();
        var stats = new BotStats(time);

        stats.StartRun();
        time.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(30), stats.Elapsed);
    }

    [Fact]
    public void Elapsed_WhilePaused_StaysFrozen()
    {
        var time = new FakeTimeProvider();
        var stats = new BotStats(time);

        stats.StartRun();
        time.Advance(TimeSpan.FromSeconds(30));
        stats.PauseRun();
        time.Advance(TimeSpan.FromSeconds(100));

        Assert.Equal(TimeSpan.FromSeconds(30), stats.Elapsed);
    }

    [Fact]
    public void Elapsed_AfterResume_ExcludesPausedGap()
    {
        var time = new FakeTimeProvider();
        var stats = new BotStats(time);

        stats.StartRun();
        time.Advance(TimeSpan.FromSeconds(30));
        stats.PauseRun();
        time.Advance(TimeSpan.FromSeconds(100));
        stats.ResumeRun();
        time.Advance(TimeSpan.FromSeconds(20));

        Assert.Equal(TimeSpan.FromSeconds(50), stats.Elapsed);
    }

    [Fact]
    public void StartRun_CalledTwice_FirstCallWins()
    {
        var time = new FakeTimeProvider();
        var stats = new BotStats(time);

        stats.StartRun();
        time.Advance(TimeSpan.FromSeconds(40));
        stats.StartRun(); // no-op: must not reset the clock
        time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(45), stats.Elapsed);
    }

    [Fact]
    public void AverageRound_WithNoAttempts_IsNull()
    {
        var time = new FakeTimeProvider();
        var stats = new BotStats(time);

        stats.StartRun();
        time.Advance(TimeSpan.FromSeconds(60));

        Assert.Null(stats.AverageRound);
    }

    [Fact]
    public void AverageRound_WhenNeverStarted_IsNull()
    {
        var time = new FakeTimeProvider();
        var stats = new BotStats(time);

        stats.RecordSuccess();
        stats.RecordFailure();

        Assert.Null(stats.AverageRound);
    }

    [Fact]
    public void AverageRound_DividesElapsedByAttempts()
    {
        var time = new FakeTimeProvider();
        var stats = new BotStats(time);

        stats.StartRun();
        time.Advance(TimeSpan.FromSeconds(60));
        stats.RecordSuccess();
        stats.RecordSuccess();
        stats.RecordFailure();
        stats.RecordFailure();

        Assert.Equal(TimeSpan.FromSeconds(15), stats.AverageRound);
    }

    [Fact]
    public void GetSummary_WithTiming_MatchesSpecExample()
    {
        var time = new FakeTimeProvider();
        var stats = new BotStats(time);

        stats.StartRun();
        for (int i = 0; i < 15; i++)
        {
            stats.RecordSuccess();
        }
        for (int i = 0; i < 5; i++)
        {
            stats.RecordFailure();
        }
        // 20 attempts over 1660 s => avg 83 s (1:23), total 1660 s (27:40).
        time.Advance(TimeSpan.FromSeconds(1660));

        Assert.Equal(
            "Attempts: 20, Successful: 15 (75 %), Avg round: 1:23, Total: 27:40",
            stats.GetSummary());
    }

    [Fact]
    public void GetSummary_WhenTimerNeverStarted_OmitsTiming()
    {
        var time = new FakeTimeProvider();
        var stats = new BotStats(time);

        stats.RecordSuccess();
        stats.RecordSuccess();
        stats.RecordSuccess();
        stats.RecordFailure();
        time.Advance(TimeSpan.FromSeconds(500)); // no StartRun => must be ignored

        Assert.Equal("Attempts: 4, Successful: 3 (75 %)", stats.GetSummary());
    }

    [Fact]
    public void GetSummary_JustUnderOneHour_UsesMinutesSeconds()
    {
        var time = new FakeTimeProvider();
        var stats = new BotStats(time);

        stats.StartRun();
        stats.RecordSuccess(); // 1 attempt so avg == total
        time.Advance(TimeSpan.FromSeconds(3599)); // 59:59

        Assert.Equal(
            "Attempts: 1, Successful: 1 (100 %), Avg round: 59:59, Total: 59:59",
            stats.GetSummary());
    }

    [Fact]
    public void GetSummary_AtOneHourAndBeyond_UsesHoursMinutesSeconds()
    {
        var time = new FakeTimeProvider();
        var stats = new BotStats(time);

        stats.StartRun();
        stats.RecordSuccess(); // 1 attempt so avg == total
        time.Advance(TimeSpan.FromSeconds(3723)); // 1:02:03

        Assert.Equal(
            "Attempts: 1, Successful: 1 (100 %), Avg round: 1:02:03, Total: 1:02:03",
            stats.GetSummary());
    }
}
