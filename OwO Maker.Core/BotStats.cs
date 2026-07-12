using System.Globalization;

namespace OwO_Maker.Core;

/// <summary>
/// Thread-safe counter of minigame round outcomes. A worker thread records
/// results while the GUI thread reads the properties and summary.
/// </summary>
public class BotStats
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private int _successes;
    private int _failures;
    private bool _started;
    private bool _paused;
    private long _segmentStart;
    private TimeSpan _accumulated;

    public BotStats() : this(TimeProvider.System)
    {
    }

    public BotStats(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public TimeSpan Elapsed
    {
        get { lock (_gate) { return ElapsedNoLock(); } }
    }

    private TimeSpan ElapsedNoLock()
    {
        if (!_started)
        {
            return TimeSpan.Zero;
        }

        if (_paused)
        {
            return _accumulated;
        }

        return _accumulated + _timeProvider.GetElapsedTime(_segmentStart);
    }

    public void StartRun()
    {
        lock (_gate)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _paused = false;
            _accumulated = TimeSpan.Zero;
            _segmentStart = _timeProvider.GetTimestamp();
        }
    }

    public void PauseRun()
    {
        lock (_gate)
        {
            if (!_started || _paused)
            {
                return;
            }

            _accumulated += _timeProvider.GetElapsedTime(_segmentStart);
            _paused = true;
        }
    }

    public TimeSpan? AverageRound
    {
        get
        {
            lock (_gate)
            {
                int attempts = _successes + _failures;
                if (!_started || attempts == 0)
                {
                    return null;
                }

                return ElapsedNoLock() / attempts;
            }
        }
    }

    public void ResumeRun()
    {
        lock (_gate)
        {
            if (!_started || !_paused)
            {
                return;
            }

            _segmentStart = _timeProvider.GetTimestamp();
            _paused = false;
        }
    }

    public int Successes
    {
        get { lock (_gate) { return _successes; } }
    }

    public int Failures
    {
        get { lock (_gate) { return _failures; } }
    }

    public int Attempts
    {
        get { lock (_gate) { return _successes + _failures; } }
    }

    public double SuccessRate
    {
        get
        {
            lock (_gate)
            {
                int attempts = _successes + _failures;
                return attempts == 0 ? 0.0 : (double)_successes / attempts;
            }
        }
    }

    public void RecordSuccess()
    {
        lock (_gate) { _successes++; }
    }

    public void RecordFailure()
    {
        lock (_gate) { _failures++; }
    }

    public string GetSummary()
    {
        int successes, attempts;
        bool started;
        TimeSpan elapsed, average;
        lock (_gate)
        {
            successes = _successes;
            attempts = _successes + _failures;
            started = _started;
            elapsed = ElapsedNoLock();
            average = attempts == 0 ? TimeSpan.Zero : elapsed / attempts;
        }

        double rate = attempts == 0 ? 0.0 : (double)successes / attempts;
        int percent = (int)Math.Round(rate * 100, MidpointRounding.AwayFromZero);

        string summary = string.Format(
            CultureInfo.InvariantCulture,
            "Attempts: {0}, Successful: {1} ({2} %)",
            attempts, successes, percent);

        if (started && attempts > 0)
        {
            summary += string.Format(
                CultureInfo.InvariantCulture,
                ", Avg round: {0}, Total: {1}",
                FormatDuration(average), FormatDuration(elapsed));
        }

        return summary;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        long totalSeconds = (long)Math.Round(
            duration.TotalSeconds, MidpointRounding.AwayFromZero);

        long hours = totalSeconds / 3600;
        long minutes = (totalSeconds % 3600) / 60;
        long seconds = totalSeconds % 60;

        return hours > 0
            ? string.Format(
                CultureInfo.InvariantCulture, "{0}:{1:D2}:{2:D2}", hours, minutes, seconds)
            : string.Format(
                CultureInfo.InvariantCulture, "{0}:{1:D2}", minutes, seconds);
    }
}
