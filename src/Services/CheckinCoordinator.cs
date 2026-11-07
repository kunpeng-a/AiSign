using System;
using System.Collections.Generic;
using System.Threading;
using AiSign.Core;
using AiSign.Platforms;
using AiSign.Stores;

namespace AiSign.Services
{
    internal sealed class CheckinCoordinator
    {
        private readonly AccountStore _accounts;
        private readonly SecretVault _vault;
        private readonly ResultStore _results;
        private readonly SchedulerStateStore _state;
        private readonly SettingsStore _settings;
        private readonly object _lock = new object();
        private readonly Dictionary<string, SemaphoreSlim> _runLocks = new Dictionary<string, SemaphoreSlim>();

        public event Action<Account, CheckinOutcome> Completed;

        public CheckinCoordinator(AccountStore accounts, SecretVault vault, ResultStore results, SchedulerStateStore state, SettingsStore settings)
        {
            _accounts = accounts;
            _vault = vault;
            _results = results;
            _state = state;
            _settings = settings;
        }

        public CheckinOutcome RunAccount(Account account, bool dryRun, bool manual)
        {
            if (account == null) return CheckinOutcome.Make(OutcomeKind.Permanent, 0, "账号不存在");
            SemaphoreSlim runLock = LockFor(account.Id);
            if (!runLock.Wait(0))
            {
                if (!manual)
                    return CheckinOutcome.Make(OutcomeKind.Transient, 0, "该账号正在签到中");
                if (!runLock.Wait(TimeSpan.FromMinutes(2)))
                    return CheckinOutcome.Make(OutcomeKind.Transient, 0, "等待账号锁超时");
            }

            try
            {
                return RunLocked(account, dryRun, manual);
            }
            finally
            {
                runLock.Release();
            }
        }

        private CheckinOutcome RunLocked(Account account, bool dryRun, bool manual)
        {
            ICheckinPlatform platform = PlatformRegistry.Get(account.Platform);
            if (platform == null)
                return CheckinOutcome.Make(OutcomeKind.Permanent, 0, "未知平台：" + account.Platform);

            SecretPayload secret = _vault.Get(account.Id);
            if (secret == null)
            {
                _accounts.SetAuthState(account.Id, AccountState.Locked);
                CheckinOutcome missing = CheckinOutcome.Make(OutcomeKind.AuthRequired, -1, "凭证无法读取，请重新添加账号");
                if (!dryRun) Record(account, missing, 1);
                return missing;
            }

            DateTime started = DateTime.Now;
            CheckinOutcome outcome;
            try
            {
                outcome = platform.Run(account, secret, delegate(SecretPayload updated)
                {
                    if (_vault.Save(account.Id, updated))
                    {
                        Account a = _accounts.Get(account.Id);
                        if (a != null) a.CredentialUpdatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
                    }
                }, dryRun);
            }
            catch (Exception ex)
            {
                Logger.Error(account.Platform, "签到异常 " + account.Id, ex);
                outcome = CheckinOutcome.Make(OutcomeKind.Transient, -3, "签到异常：" + ex.Message);
            }

            if (outcome == null)
                outcome = CheckinOutcome.Make(OutcomeKind.Transient, -3, "签到无返回");

            if (!dryRun)
            {
                Record(account, outcome, 1);
                UpdateState(account, outcome);
                if (outcome.Kind == OutcomeKind.AuthRequired)
                    _accounts.SetAuthState(account.Id, AccountState.NeedsReauth);
                else if (outcome.Kind == OutcomeKind.Success || outcome.Kind == OutcomeKind.Already ||
                         outcome.Kind == OutcomeKind.SuccessUnconfirmed)
                    _accounts.SetAuthState(account.Id, AccountState.Ok);
                Logger.Info(account.Platform, account.Id + " -> " + outcome.Kind + " code=" + outcome.Code + " " + outcome.Message);
            }

            Action<Account, CheckinOutcome> handler = Completed;
            if (handler != null)
            {
                try { handler(account, outcome); }
                catch (Exception ex) { Logger.Warn("ui", "签到回调异常: " + ex.Message); }
            }
            return outcome;
        }

        private void Record(Account account, CheckinOutcome outcome, int attempts)
        {
            CheckinRecord r = new CheckinRecord();
            r.Platform = account.Platform;
            r.AccountId = account.Id;
            r.Name = account.Name;
            r.Status = SchedulerPolicy.StatusOf(outcome.Kind);
            r.Code = outcome.Code;
            r.Message = outcome.Message;
            r.Credits = outcome.Credits;
            AccountRunState st = _state.Get(account.Id);
            r.Attempts = st == null ? attempts : Math.Max(attempts, st.Attempts + 1);
            r.Confirmed = outcome.Confirmed;
            r.StartedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            r.FinishedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            _results.Record(r);
        }

        private void UpdateState(Account account, CheckinOutcome outcome)
        {
            AccountRunState old = _state.Get(account.Id);
            AccountRunState s = new AccountRunState();
            s.Attempts = old == null ? 1 : old.Attempts + 1;
            s.Status = SchedulerPolicy.StateStatusOf(outcome.Kind);
            s.LastCode = outcome.Code;
            s.LastMessage = outcome.Message;
            s.UpdatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

            if (outcome.Kind == OutcomeKind.Transient || outcome.Kind == OutcomeKind.Pending ||
                outcome.Kind == OutcomeKind.SuccessUnconfirmed)
            {
                Settings settings = _settings == null ? null : _settings.Current;
                DateTime? retry = SchedulerPolicy.NextRetry(DateTime.Now, s.Attempts, settings);
                if (retry.HasValue) s.NextRetryAt = retry.Value.ToString("yyyy-MM-ddTHH:mm:ss");
            }
            _state.Set(account.Id, s);
        }

        private SemaphoreSlim LockFor(string id)
        {
            lock (_lock)
            {
                SemaphoreSlim s;
                if (!_runLocks.TryGetValue(id, out s))
                {
                    s = new SemaphoreSlim(1, 1);
                    _runLocks[id] = s;
                }
                return s;
            }
        }
    }
}
