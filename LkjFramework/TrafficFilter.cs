using System;

namespace LkjFramework
{
    public struct TrafficFilter
    {
        private long tick_;
        private int counter_;

        private long GetTime()
        {
            return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        }

        public bool Attempt(long period, int advance, int limit)
        {
            long current = GetTime();

            if (current - tick_ > period)
            {
                tick_ = current;
                counter_ = 0;
            }

            return (counter_ += advance) <= limit;
        }
    }
}
