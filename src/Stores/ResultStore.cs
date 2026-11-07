using System;
using System.Collections.Generic;
using System.Globalization;
using AiSign.Core;

namespace AiSign.Stores
{
    internal sealed class ResultStore
    {
        private readonly object _lock = new object();
        private ResultsFile _file;

        public ResultStore()
        {
            _file = JsonStore.Load<ResultsFile>(AppPaths.ResultsFile, NewFile);
            if (_file.Days == null) _file.Days = new Dictionary<string, Dictionary<string, CheckinRecord>>();
        }

        private static ResultsFile NewFile()
        {
            return new ResultsFile();
        }

        public void Record(CheckinRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.AccountId)) return;
            string day = DayOf(record.FinishedAt);
            if (string.IsNullOrEmpty(day)) day = DateTime.Now.ToString("yyyy-MM-dd");
            lock (_lock)
            {
                Dictionary<string, CheckinRecord> dayMap;
                if (!_file.Days.TryGetValue(day, out dayMap) || dayMap == null)
                {
                    dayMap = new Dictionary<string, CheckinRecord>();
                    _file.Days[day] = dayMap;
                }
                dayMap[record.AccountId] = record;
                PruneLocked(180);
                JsonStore.Save(AppPaths.ResultsFile, _file);
            }
        }

        public CheckinRecord Get(string day, string accountId)
        {
            lock (_lock)
            {
                Dictionary<string, CheckinRecord> dayMap;
                if (_file.Days.TryGetValue(day, out dayMap) && dayMap != null)
                {
                    CheckinRecord r;
                    if (dayMap.TryGetValue(accountId, out r)) return r;
                }
            }
            return null;
        }

        public Dictionary<string, CheckinRecord> Day(string day)
        {
            lock (_lock)
            {
                Dictionary<string, CheckinRecord> dayMap;
                if (_file.Days.TryGetValue(day, out dayMap) && dayMap != null)
                    return new Dictionary<string, CheckinRecord>(dayMap);
            }
            return new Dictionary<string, CheckinRecord>();
        }

        public List<CheckinRecord> Range(string accountId, DateTime start, DateTime end)
        {
            List<CheckinRecord> result = new List<CheckinRecord>();
            lock (_lock)
            {
                DateTime d = start.Date;
                while (d <= end.Date)
                {
                    string key = d.ToString("yyyy-MM-dd");
                    Dictionary<string, CheckinRecord> dayMap;
                    if (_file.Days.TryGetValue(key, out dayMap) && dayMap != null)
                    {
                        CheckinRecord r;
                        if (dayMap.TryGetValue(accountId, out r)) result.Add(r);
                    }
                    d = d.AddDays(1);
                }
            }
            return result;
        }

        public void Clear()
        {
            lock (_lock)
            {
                _file.Days.Clear();
                JsonStore.Save(AppPaths.ResultsFile, _file);
            }
        }

        private void PruneLocked(int keepDays)
        {
            if (_file.Days.Count == 0) return;
            DateTime cutoff = DateTime.Now.Date.AddDays(-keepDays);
            List<string> remove = new List<string>();
            foreach (string key in _file.Days.Keys)
            {
                DateTime d;
                if (!DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out d) || d < cutoff)
                    remove.Add(key);
            }
            for (int i = 0; i < remove.Count; i++) _file.Days.Remove(remove[i]);
        }

        private static string DayOf(string iso)
        {
            if (string.IsNullOrEmpty(iso) || iso.Length < 10) return null;
            return iso.Substring(0, 10);
        }
    }
}
