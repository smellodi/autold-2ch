using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace Olfactory.Utils
{
    public class DispatchOnce : DispatcherTimer
    {
        public DispatchOnce(double seconds, Action action, bool start = true) : base()
        {
            _actions.Enqueue(new ScheduledAction() { Pause = seconds, Action = action });

            Interval = TimeSpan.FromSeconds(seconds);
            Tick += (s, e) => Execute();

            if (start)
            {
                Start();
            }
        }

        public static DispatchOnce Do(double seconds, Action action)
        {
            return new DispatchOnce(seconds, action);
        }

        public DispatchOnce Then(double seconds, Action action)
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

        Queue<ScheduledAction> _actions = new Queue<ScheduledAction>();

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
    
    public class DelayedAction : System.Timers.Timer
    {
        public DelayedAction(int milliseconds, Action action, bool start = true) : base()
        {
            _actions.Enqueue(new ScheduledAction() { Pause = milliseconds, Action = action });

            Interval = milliseconds;
            AutoReset = false;

            Elapsed += (s, e) => Execute();

            if (start)
            {
                Start();
            }
        }

        public static DelayedAction Do(int milliseconds, Action action)
        {
            return new DelayedAction(milliseconds, action);
        }

        public DelayedAction Then(int milliseconds, Action action)
        {
            _actions.Enqueue(new ScheduledAction() { Pause = milliseconds, Action = action });
            return this;
        }


        // Internal

        struct ScheduledAction
        {
            public int Pause;
            public Action Action;
        }

        Queue<ScheduledAction> _actions = new Queue<ScheduledAction>();

        private void Execute()
        {
            Stop();

            var action = _actions.Dequeue();
            Dispatcher.CurrentDispatcher.Invoke(() => action.Action());

            if (_actions.Count > 0)
            {
                var next = _actions.Peek();
                Interval = next.Pause;
                Start();
            }
        }
    }
}
