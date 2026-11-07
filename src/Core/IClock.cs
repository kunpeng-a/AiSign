using System;

namespace AiSign.Core
{
    internal interface IClock
    {
        DateTime Now { get; }
    }

    internal sealed class SystemClock : IClock
    {
        public DateTime Now { get { return DateTime.Now; } }
    }

    internal sealed class FakeClock : IClock
    {
        private DateTime _now;
        public FakeClock(DateTime start) { _now = start; }
        public DateTime Now { get { return _now; } set { _now = value; } }
        public void Advance(TimeSpan span) { _now = _now.Add(span); }
    }
}
