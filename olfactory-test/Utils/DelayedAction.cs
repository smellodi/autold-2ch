using System;
using System.Windows.Threading;

namespace Utils
{
    public class DispatchOnce : DispatcherTimer
    {
        public DispatchOnce(double seconds, Action action, bool start = true) : base()
        {
            Interval = TimeSpan.FromSeconds(seconds);
            Tick += (s, e) =>
            {
                Stop();
                action();
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
