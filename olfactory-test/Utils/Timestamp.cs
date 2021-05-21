using System;

namespace Olfactory.Utils
{
    public static class Timestamp
    {
        /// <summary>
        /// Current timestamp to be used everywhere to get syncronized records
        /// </summary>
        public static long Value { get { return (DateTime.Now.Ticks - _start) / 10000; } }

        // Internal

        static readonly long _start = DateTime.Now.Ticks;
    }
}
