using System;
using System.Threading;

namespace AiSign.Core
{
    internal sealed class SingleInstance : IDisposable
    {
        private const string MutexName = "Local\\AiSign_6d1f8a2c";
        private const string EventName = "Local\\AiSign_Show_6d1f8a2c";

        private Mutex _mutex;
        private EventWaitHandle _event;
        private Thread _waiter;
        private volatile bool _disposed;

        public bool IsFirst { get; private set; }
        public event Action Activated;

        public static SingleInstance Acquire()
        {
            SingleInstance s = new SingleInstance();
            bool created;
            s._mutex = new Mutex(true, MutexName, out created);
            s.IsFirst = created;
            s._event = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            if (created)
            {
                s._waiter = new Thread(s.WaitLoop);
                s._waiter.IsBackground = true;
                s._waiter.Name = "AiSign.SingleInstance";
                s._waiter.Start();
            }
            return s;
        }

        public void SignalExisting()
        {
            try { _event.Set(); } catch { }
        }

        private void WaitLoop()
        {
            while (!_disposed)
            {
                try
                {
                    if (_event.WaitOne(1000))
                    {
                        Action a = Activated;
                        if (a != null) a();
                    }
                }
                catch { }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            try { if (_event != null) _event.Set(); } catch { }
            try { if (_waiter != null) _waiter.Join(800); } catch { }
            try { if (_event != null) _event.Close(); } catch { }
            try { if (_mutex != null) { _mutex.ReleaseMutex(); _mutex.Close(); } } catch { }
            _event = null;
            _mutex = null;
        }
    }
}
