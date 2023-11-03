using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace AutOlD2Ch.Utils;

public class DispatchOnceUI : DispatcherTimer
{
    public DispatchOnceUI(double seconds, Action action, bool start = true) : base()
    {
        _actions.Enqueue(new ScheduledAction() { Pause = seconds, Action = action });

        Interval = TimeSpan.FromSeconds(seconds);
        Tick += (s, e) => Execute();

        if (start)
        {
            Start();
        }
    }

    public static DispatchOnceUI? Do(double seconds, Action action)
    {
        if (seconds > 0)
        {
            return new DispatchOnceUI(seconds, action);
        }
        else
        {
            action();
            return null;
        }
    }

    public DispatchOnceUI Then(double seconds, Action action)
    {
        _actions.Enqueue(new ScheduledAction() { Pause = seconds, Action = action });
        return this;
    }


    // Internal

    struct ScheduledAction
    {
        public double Pause;
        public Action Action;
    }

    readonly Queue<ScheduledAction> _actions = new();

    private void Execute()
    {
        Stop();

        var action = _actions.Dequeue();
        action.Action();

        if (_actions.Count > 0)
        {
            var next = _actions.Peek();
            Interval = TimeSpan.FromSeconds(next.Pause);
            Start();
        }
    }
}

public class DispatchOnce : System.Timers.Timer
{
    public DispatchOnce(double seconds, Action action, bool start = true) : base()
    {
        var pause = (int)(1000 * seconds);
        _actions.Enqueue(new ScheduledAction() { Pause = pause, Action = action });

        Interval = pause;
        AutoReset = false;

        Elapsed += (s, e) => Execute();

        if (start)
        {
            Start();
        }
    }

    public static DispatchOnce? Do(double seconds, Action action)
    {
        if (seconds > 0)
        {
            return new DispatchOnce(seconds, action);
        }
        else
        {
            action();
            return null;
        }
    }

    /// <summary>
    /// Adds an action to the chain of actions
    /// </summary>
    /// <param name="seconds">time to wait</param>
    /// <param name="action">action to execute</param>
    /// <returns></returns>
    public DispatchOnce Then(double seconds, Action action)
    {
        _actions.Enqueue(new ScheduledAction() { Pause = (int)(1000 * seconds), Action = action });
        return this;
    }

    /// <summary>
    /// Stop execution until Resume is called, or a timeout elapses
    /// </summary>
    /// <param name="maxTime">timeout in seconds</param>
    /// <returns>The instance</returns>
    public DispatchOnce ThenWait(double maxTime = double.MaxValue)
    {
        _actions.Enqueue(new ScheduledAction() { Pause = maxTime == double.MaxValue ? int.MaxValue : (int)(1000 * maxTime), Action = null });
        return this;
    }

    /// <summary>
    /// Interrupts waiting for current task to execute (i.e. this task is abandoned) and starts waiting for the next tasks.
    /// Also resumes the time after <see cref="ThenWait(double)"/> was called.
    /// </summary>
    public void Resume()
    {
        if (Enabled)
        {
            Stop();
            _actions.Dequeue();
        }

        if (_actions.Count > 0)
        {
            var next = _actions.Peek();
            Interval = next.Pause;
            Start();
        }
    }


    // Internal

    struct ScheduledAction
    {
        public int Pause;
        public Action? Action;
    }

    readonly Queue<ScheduledAction> _actions = new();

    private void Execute()
    {
        Stop();

        var action = _actions.Dequeue();
        action.Action?.Invoke();

        if (_actions.Count > 0)
        {
            var next = _actions.Peek();
            Interval = next.Pause;
            Start();
        }
    }
}
