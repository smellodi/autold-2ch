using System;

namespace Olfactory.Utils
{
    /// <summary>
    /// Current timestamp to be used everywhere to get syncronized records
    /// </summary>
    public static class Timestamp
    {
        /// <summary>
        /// In milliseconds
        /// </summary>
        public static long Ms { get { return (DateTime.Now.Ticks - _start) / 10000; } }

        /// <summary>
        /// In seconds
        /// </summary>
        public static double Sec { get { return (double)(DateTime.Now.Ticks - _start) / 10000000; } }

        // Internal

        static readonly long _start = DateTime.Now.Ticks;
    }
}
