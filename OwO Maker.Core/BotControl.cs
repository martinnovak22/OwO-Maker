namespace OwO_Maker.Core;

public class BotControl
{
    private readonly object _lock = new();
    private BotState _state = BotState.Created;

    // Signaled (set) whenever the bot is NOT paused; reset while paused so
    // WaitIfPaused blocks. Set again on Resume or Stop to release waiters.
    private readonly ManualResetEventSlim _notPausedGate = new(initialState: true);

    // Async counterpart of the gate: a Task that is complete whenever the bot is NOT
    // paused. On Pause it is swapped for a fresh incomplete source; Resume/Stop complete
    // it, releasing any awaiters. No polling.
    private TaskCompletionSource<bool> _resumeSource = CreateCompletedSource();

    private static TaskCompletionSource<bool> CreateCompletedSource()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        tcs.SetResult(true);
        return tcs;
    }

    /// <summary>
    /// Raised after every successful transition, with the new state. Never raised for rejected transitions.
    /// </summary>
    public event Action<BotState>? StateChanged;

    public BotState State
    {
        get { lock (_lock) { return _state; } }
    }

    public bool ShouldContinue
    {
        get { lock (_lock) { return _state != BotState.Stopped; } }
    }

    public bool Start()
    {
        lock (_lock)
        {
            if (_state != BotState.Created)
                return false;
            _state = BotState.Running;
        }
        StateChanged?.Invoke(BotState.Running);
        return true;
    }

    public bool Pause()
    {
        lock (_lock)
        {
            if (_state != BotState.Running)
                return false;
            _state = BotState.Paused;
            _notPausedGate.Reset();
            _resumeSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        StateChanged?.Invoke(BotState.Paused);
        return true;
    }

    public bool Resume()
    {
        lock (_lock)
        {
            if (_state != BotState.Paused)
                return false;
            _state = BotState.Running;
            _notPausedGate.Set();
            _resumeSource.TrySetResult(true);
        }
        StateChanged?.Invoke(BotState.Running);
        return true;
    }

    public bool Stop()
    {
        lock (_lock)
        {
            if (_state == BotState.Stopped)
                return false;
            _state = BotState.Stopped;
            _notPausedGate.Set();
            _resumeSource.TrySetResult(true);
        }
        StateChanged?.Invoke(BotState.Stopped);
        return true;
    }

    /// <summary>
    /// Returns immediately unless the bot is Paused; while Paused, blocks the calling
    /// thread until Resume() or Stop() is called.
    /// </summary>
    public void WaitIfPaused()
    {
        _notPausedGate.Wait();
    }

    /// <summary>
    /// Completes immediately unless the bot is Paused; while Paused, the returned Task
    /// stays incomplete until Resume() or Stop() is called. Suitable for async bot loops
    /// running on thread-pool threads (does not block a pool thread).
    /// </summary>
    public Task WaitIfPausedAsync()
    {
        lock (_lock)
        {
            return _resumeSource.Task;
        }
    }
}
