using System.Threading.Tasks;
using OwO_Maker.Core;

namespace OwO_Maker.Core.Tests;

public class BotStatsTests
{
    [Fact]
    public void RecordSuccess_IncrementsSuccesses()
    {
        var stats = new BotStats();

        stats.RecordSuccess();

        Assert.Equal(1, stats.Successes);
    }

    [Fact]
    public void RecordFailure_IncrementsFailures()
    {
        var stats = new BotStats();

        stats.RecordFailure();

        Assert.Equal(1, stats.Failures);
    }

    [Fact]
    public void Attempts_IsSuccessesPlusFailures()
    {
        var stats = new BotStats();

        stats.RecordSuccess();
        stats.RecordSuccess();
        stats.RecordFailure();

        Assert.Equal(3, stats.Attempts);
    }

    [Fact]
    public void SuccessRate_WithNoAttempts_IsZero()
    {
        var stats = new BotStats();

        Assert.Equal(0.0, stats.SuccessRate);
    }

    [Fact]
    public void SuccessRate_WithThreeOfFour_IsPointSevenFive()
    {
        var stats = new BotStats();

        stats.RecordSuccess();
        stats.RecordSuccess();
        stats.RecordSuccess();
        stats.RecordFailure();

        Assert.Equal(0.75, stats.SuccessRate);
    }

    [Fact]
    public void GetSummary_WithNoAttempts_ReturnsZeroSummary()
    {
        var stats = new BotStats();

        Assert.Equal("Attempts: 0, Successful: 0 (0 %)", stats.GetSummary());
    }

    [Fact]
    public void GetSummary_WithFifteenOfTwenty_MatchesSpecExample()
    {
        var stats = new BotStats();

        for (int i = 0; i < 15; i++)
        {
            stats.RecordSuccess();
        }
        for (int i = 0; i < 5; i++)
        {
            stats.RecordFailure();
        }

        Assert.Equal("Attempts: 20, Successful: 15 (75 %)", stats.GetSummary());
    }

    [Fact]
    public void GetSummary_AtMidpoint_RoundsAwayFromZero()
    {
        var stats = new BotStats();

        stats.RecordSuccess();
        for (int i = 0; i < 7; i++)
        {
            stats.RecordFailure();
        }

        // 1 of 8 attempts = 12.5 %, which must round to 13 (away from zero), not 12.
        Assert.Equal("Attempts: 8, Successful: 1 (13 %)", stats.GetSummary());
    }

    [Fact]
    public void RecordFromManyThreads_KeepsExactTotals()
    {
        var stats = new BotStats();

        Parallel.For(0, 1000, _ => stats.RecordSuccess());
        Parallel.For(0, 1000, _ => stats.RecordFailure());

        Assert.Equal(1000, stats.Successes);
        Assert.Equal(1000, stats.Failures);
        Assert.Equal(2000, stats.Attempts);
    }
}
