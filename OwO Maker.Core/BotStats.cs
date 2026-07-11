using System.Globalization;

namespace OwO_Maker.Core;

/// <summary>
/// Thread-safe counter of minigame round outcomes. A worker thread records
/// results while the GUI thread reads the properties and summary.
/// </summary>
public class BotStats
{
    private readonly object _gate = new();
    private int _successes;
    private int _failures;

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
        lock (_gate)
        {
            successes = _successes;
            attempts = _successes + _failures;
        }

        double rate = attempts == 0 ? 0.0 : (double)successes / attempts;
        int percent = (int)Math.Round(rate * 100, MidpointRounding.AwayFromZero);

        return string.Format(
            CultureInfo.InvariantCulture,
            "Attempts: {0}, Successful: {1} ({2} %)",
            attempts, successes, percent);
    }
}
