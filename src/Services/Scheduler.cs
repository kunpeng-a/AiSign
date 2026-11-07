using System;
using System.Collections.Generic;
using System.Threading;
using AiSign.Core;
using AiSign.Stores;

namespace AiSign.Services
{
    internal sealed class Scheduler : IDisposable
    {
        private readonly SettingsStore _settings;
        private readonly AccountStore _accounts;
        private readonly SchedulerStateStore _state;
        private readonly CheckinCoordinator _coordinator;
        private readonly object _lock = new object();

        private Timer _timer;
        private int _busy;
        private DateTime _startAt;
        private string _givenUpDay;
        private volatile bool _disposed;

        public event Action<Account, CheckinOutcome> AccountCompleted;
        public event Action<int, int> BatchCompleted;
        public event Action<string> StatusChanged;

        public Scheduler(SettingsStore settings, AccountStore accounts, SchedulerStateStore state, CheckinCoordinator coordinator)
        {
            _settings = settings;
            _accounts = accounts;
            _state = state;
            _coordinator = coordinator;
            _coordinator.Completed += OnCoordinatorCompleted;
        }

        public void Start()
        {
            Settings s = _settings.Current;
            _startAt = DateTime.Now.AddSeconds(Math.Max(0, s.StartupDelaySec));
            try { Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged; } catch { }
            _timer = new Timer(Tick, null, 5000, 30000);
            Logger.Info("sched", "调度器启动，首次放行 " + _startAt.ToString("HH:mm:ss"));
        }

        public void TriggerSoon()
        {
            Timer t = _timer;
            if (t != null) { try { t.Change(500, 30000); } catch { } }
        }

        private void OnPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            if (e.Mode == Microsoft.Win32.PowerModes.Resume)
            {
                Logger.Info("sched", "检测到系统唤醒，准备补跑");
                TriggerSoon();
            }
        }

        private void Tick(object ignored)
        {
            if (_disposed) return;
            if (Interlocked.Exchange(ref _busy, 1) == 1) return;
            try
            {
                DateTime now = DateTime.Now;
                if (now < _startAt) return;
                _state.EnsureDay(now);

                Settings settings = _settings.Current;
                List<Account> accounts = _accounts.All();

                if (now.Hour >= settings.Retry.GiveUpHour)
                {
                    MarkGivenUp(now, accounts);
                    return;
                }
                if (!settings.AutoCheckin) return;

                List<Account> due = new List<Account>();
                for (int i = 0; i < accounts.Count; i++)
                {
                    Account a = accounts[i];
                    AccountRunState st = _state.Get(a.Id);
                    if (SchedulerPolicy.ShouldRun(now, a, settings, st)) due.Add(a);
                }
                if (due.Count == 0) return;

                int ok = 0;
                for (int i = 0; i < due.Count; i++)
                {
                    if (_disposed) break;
                    CheckinOutcome o = _coordinator.RunAccount(due[i], false, false);
                    if (o != null && (o.Kind == OutcomeKind.Success || o.Kind == OutcomeKind.Already ||
                        o.Kind == OutcomeKind.SuccessUnconfirmed)) ok++;
                    if (i < due.Count - 1 && settings.AccountGapMs > 0)
                    {
                        try { Thread.Sleep(settings.AccountGapMs); } catch { }
                    }
                }
                _state.SetLastTick(now);
                FireBatch(due.Count, ok);
            }
            catch (Exception ex)
            {
                Logger.Error("sched", "调度 tick 异常", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        }

        private void MarkGivenUp(DateTime now, List<Account> accounts)
        {
            string day = now.ToString("yyyy-MM-dd");
            if (_givenUpDay == day) return;
            _givenUpDay = day;
            for (int i = 0; i < accounts.Count; i++)
            {
                Account a = accounts[i];
                if (!a.Enabled) continue;
                AccountRunState st = _state.Get(a.Id);
                if (st == null) st = new AccountRunState();
                if (st.Status == CheckinStatus.Success || st.Status == CheckinStatus.Already ||
                    st.Status == CheckinStatus.Occupied || st.Status == CheckinStatus.Auth ||
                    st.Status == CheckinStatus.GivenUp) continue;
                st.Status = CheckinStatus.GivenUp;
                st.LastMessage = "已过 23:00，今日放弃";
                st.UpdatedAt = now.ToString("yyyy-MM-ddTHH:mm:ss");
                _state.Set(a.Id, st);
            }
            _state.SetLastTick(now);
            FireStatus("今日窗口已结束");
        }

        public void RunNowAsync(bool dryRun, string platformId, string accountId, Action<int, int> done)
        {
            ThreadPool.QueueUserWorkItem(delegate(object ignored)
            {
                int total = 0;
                int ok = 0;
                try
                {
                    List<Account> accounts = _accounts.All();
                    for (int i = 0; i < accounts.Count; i++)
                    {
                        Account a = accounts[i];
                        if (!a.Enabled) continue;
                        if (!string.IsNullOrEmpty(platformId) && a.Platform != platformId) continue;
                        if (!string.IsNullOrEmpty(accountId) && a.Id != accountId) continue;
                        total++;
                        CheckinOutcome o = _coordinator.RunAccount(a, dryRun, true);
                        if (o != null && (o.Kind == OutcomeKind.Success || o.Kind == OutcomeKind.Already ||
                            o.Kind == OutcomeKind.SuccessUnconfirmed)) ok++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("sched", "手动签到异常", ex);
                }
                FireBatch(total, ok);
                if (done != null)
                {
                    try { done(total, ok); } catch { }
                }
            });
        }

        private void OnCoordinatorCompleted(Account account, CheckinOutcome outcome)
        {
            Action<Account, CheckinOutcome> h = AccountCompleted;
            if (h != null)
            {
                try { h(account, outcome); } catch { }
            }
        }

        private void FireBatch(int total, int ok)
        {
            Action<int, int> h = BatchCompleted;
            if (h != null)
            {
                try { h(total, ok); } catch { }
            }
        }

        private void FireStatus(string text)
        {
            Action<string> h = StatusChanged;
            if (h != null)
            {
                try { h(text); } catch { }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            try { Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged; } catch { }
            if (_timer != null) { _timer.Dispose(); _timer = null; }
        }
    }
}
