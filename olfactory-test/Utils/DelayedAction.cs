using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace Utils
{
    public class DispatchOnce : DispatcherTimer
    {
        public DispatchOnce(double seconds, Action action, bool start = true) : base()
        {
            _actions.Enqueue(new Delayed() { Pause = seconds, Action = action });

            Interval = TimeSpan.FromSeconds(seconds);
            Tick += (s, e) =>
            {
                Execute();
            };

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
            _actions.Enqueue(new Delayed() { Pause = seconds, Action = action });
            return this;
        }


        // Internal

        struct Delayed
        {
            public double Pause;
            public Action Action;
        }

        Queue<Delayed> _actions = new Queue<Delayed>();

        private void Execute()
        {
            Stop();

            var delayed = _actions.Dequeue();
            delayed.Action();

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
        public DelayedAction(double seconds, Action action, bool start = true) : base()
        {
            Interval = (int)(seconds * 1000);
            AutoReset = false;
            Elapsed += (s, e) =>
            {
                action();
            };

            if (start)
            {
                Start();
            }
        }

        public static DelayedAction Do(double seconds, Action action)
        {
            return new DelayedAction(seconds, action);
        }
    }
}
