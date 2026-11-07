using System;
using System.Collections.Generic;
using AiSign.Core;

namespace AiSign.Stores
{
    internal sealed class SchedulerStateStore
    {
        private readonly object _lock = new object();
        private SchedulerStateFile _file;
        private string _day;

        public SchedulerStateStore()
        {
            _file = JsonStore.Load<SchedulerStateFile>(AppPaths.StateFile, NewFile);
            if (_file.Accounts == null) _file.Accounts = new Dictionary<string, AccountRunState>();
            _day = DateTime.Now.ToString("yyyy-MM-dd");
            if (_file.Day != _day)
            {
                _file.Day = _day;
                _file.Accounts.Clear();
                _file.LastTickAt = null;
            }
        }

        private static SchedulerStateFile NewFile()
        {
            SchedulerStateFile f = new SchedulerStateFile();
            f.Day = DateTime.Now.ToString("yyyy-MM-dd");
            return f;
        }

        public void EnsureDay(DateTime now)
        {
            string day = now.ToString("yyyy-MM-dd");
            lock (_lock)
            {
                if (_file.Day == day) return;
                _file.Day = day;
                _file.Accounts.Clear();
                _file.LastTickAt = null;
                _day = day;
                SaveLocked();
            }
        }

        public AccountRunState Get(string accountId)
        {
            lock (_lock)
            {
                AccountRunState s;
                if (_file.Accounts.TryGetValue(accountId, out s)) return s;
            }
            return null;
        }

        public void Set(string accountId, AccountRunState state)
        {
            if (string.IsNullOrEmpty(accountId)) return;
            lock (_lock)
            {
                _file.Accounts[accountId] = state;
                SaveLocked();
            }
        }

        public void SetLastTick(DateTime now)
        {
            lock (_lock)
            {
                _file.LastTickAt = now.ToString("yyyy-MM-ddTHH:mm:ss");
            }
        }

        public DateTime? LastTick()
        {
            lock (_lock)
            {
                DateTime d;
                if (!string.IsNullOrEmpty(_file.LastTickAt) &&
                    DateTime.TryParse(_file.LastTickAt, out d)) return d;
            }
            return null;
        }

        public void Save()
        {
            lock (_lock) SaveLocked();
        }

        private void SaveLocked()
        {
            JsonStore.Save(AppPaths.StateFile, _file);
        }

        public Dictionary<string, AccountRunState> Snapshot()
        {
            lock (_lock) return new Dictionary<string, AccountRunState>(_file.Accounts);
        }
    }
}
