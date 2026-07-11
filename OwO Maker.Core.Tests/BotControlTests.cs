using OwO_Maker.Core;

namespace OwO_Maker.Core.Tests;

public class BotControlTests
{
    [Fact]
    public void InitialState_IsCreated()
    {
        var control = new BotControl();

        Assert.Equal(BotState.Created, control.State);
    }

    [Fact]
    public void Start_FromCreated_TransitionsToRunning_ReturnsTrue()
    {
        var control = new BotControl();

        bool result = control.Start();

        Assert.True(result);
        Assert.Equal(BotState.Running, control.State);
    }

    [Fact]
    public void Start_WhenAlreadyRunning_ReturnsFalse_StateUnchanged()
    {
        var control = new BotControl();
        control.Start();

        bool result = control.Start();

        Assert.False(result);
        Assert.Equal(BotState.Running, control.State);
    }

    [Fact]
    public void Pause_FromRunning_TransitionsToPaused_ReturnsTrue()
    {
        var control = new BotControl();
        control.Start();

        bool result = control.Pause();

        Assert.True(result);
        Assert.Equal(BotState.Paused, control.State);
    }

    [Fact]
    public void Pause_FromCreated_ReturnsFalse_StateUnchanged()
    {
        var control = new BotControl();

        bool result = control.Pause();

        Assert.False(result);
        Assert.Equal(BotState.Created, control.State);
    }

    [Fact]
    public void Resume_FromPaused_TransitionsToRunning_ReturnsTrue()
    {
        var control = new BotControl();
        control.Start();
        control.Pause();

        bool result = control.Resume();

        Assert.True(result);
        Assert.Equal(BotState.Running, control.State);
    }

    [Fact]
    public void Resume_FromRunning_ReturnsFalse_StateUnchanged()
    {
        var control = new BotControl();
        control.Start();

        bool result = control.Resume();

        Assert.False(result);
        Assert.Equal(BotState.Running, control.State);
    }

    [Fact]
    public void Stop_FromRunning_TransitionsToStopped_ReturnsTrue()
    {
        var control = new BotControl();
        control.Start();

        bool result = control.Stop();

        Assert.True(result);
        Assert.Equal(BotState.Stopped, control.State);
    }

    [Fact]
    public void Stop_FromCreated_TransitionsToStopped_ReturnsTrue()
    {
        var control = new BotControl();

        bool result = control.Stop();

        Assert.True(result);
        Assert.Equal(BotState.Stopped, control.State);
    }

    [Fact]
    public void Stop_FromPaused_TransitionsToStopped_ReturnsTrue()
    {
        var control = new BotControl();
        control.Start();
        control.Pause();

        bool result = control.Stop();

        Assert.True(result);
        Assert.Equal(BotState.Stopped, control.State);
    }

    [Fact]
    public void Stop_WhenAlreadyStopped_ReturnsFalse()
    {
        var control = new BotControl();
        control.Stop();

        bool result = control.Stop();

        Assert.False(result);
        Assert.Equal(BotState.Stopped, control.State);
    }

    [Fact]
    public void Start_AfterStop_ReturnsFalse_StaysStopped()
    {
        var control = new BotControl();
        control.Stop();

        Assert.False(control.Start());
        Assert.False(control.Pause());
        Assert.False(control.Resume());
        Assert.Equal(BotState.Stopped, control.State);
    }

    [Theory]
    [InlineData(BotState.Created)]
    [InlineData(BotState.Running)]
    [InlineData(BotState.Paused)]
    public void ShouldContinue_IsTrue_WhenNotStopped(BotState target)
    {
        var control = new BotControl();
        if (target == BotState.Running) control.Start();
        if (target == BotState.Paused) { control.Start(); control.Pause(); }

        Assert.True(control.ShouldContinue);
    }

    [Fact]
    public void ShouldContinue_IsFalse_WhenStopped()
    {
        var control = new BotControl();
        control.Stop();

        Assert.False(control.ShouldContinue);
    }

    [Fact]
    public void StateChanged_RaisedWithNewState_OnSuccessfulTransition()
    {
        var control = new BotControl();
        var observed = new List<BotState>();
        control.StateChanged += s => observed.Add(s);

        control.Start();
        control.Pause();
        control.Resume();
        control.Stop();

        Assert.Equal(
            new[] { BotState.Running, BotState.Paused, BotState.Running, BotState.Stopped },
            observed);
    }

    [Fact]
    public void StateChanged_NotRaised_OnRejectedTransition()
    {
        var control = new BotControl();
        control.Start();
        var observed = new List<BotState>();
        control.StateChanged += s => observed.Add(s);

        control.Start();   // rejected
        control.Resume();  // rejected

        Assert.Empty(observed);
    }

    [Fact]
    public void WaitIfPaused_ReturnsImmediately_WhenNotPaused()
    {
        var control = new BotControl();
        control.Start();

        var task = Task.Run(() => control.WaitIfPaused());

        Assert.True(task.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void WaitIfPaused_Blocks_WhilePaused_Unblocks_OnResume()
    {
        var control = new BotControl();
        control.Start();
        control.Pause();

        var task = Task.Run(() => control.WaitIfPaused());

        Assert.False(task.Wait(TimeSpan.FromMilliseconds(200)));

        control.Resume();

        Assert.True(task.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void WaitIfPaused_Blocks_WhilePaused_Unblocks_OnStop()
    {
        var control = new BotControl();
        control.Start();
        control.Pause();

        var task = Task.Run(() => control.WaitIfPaused());

        Assert.False(task.Wait(TimeSpan.FromMilliseconds(200)));

        control.Stop();

        Assert.True(task.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void WaitIfPausedAsync_CompletesImmediately_WhenNotPaused()
    {
        var control = new BotControl();
        control.Start();

        Task task = control.WaitIfPausedAsync();

        Assert.True(task.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void WaitIfPausedAsync_StaysIncomplete_WhilePaused_Completes_OnResume()
    {
        var control = new BotControl();
        control.Start();
        control.Pause();

        Task task = control.WaitIfPausedAsync();

        Assert.False(task.Wait(TimeSpan.FromMilliseconds(200)));

        control.Resume();

        Assert.True(task.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void WaitIfPausedAsync_StaysIncomplete_WhilePaused_Completes_OnStop()
    {
        var control = new BotControl();
        control.Start();
        control.Pause();

        Task task = control.WaitIfPausedAsync();

        Assert.False(task.Wait(TimeSpan.FromMilliseconds(200)));

        control.Stop();

        Assert.True(task.Wait(TimeSpan.FromSeconds(5)));
    }
}
