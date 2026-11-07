using System;
using System.Collections.Generic;
using AiSign.Core;
using AiSign.Platforms;
using AiSign.Stores;

namespace AiSign.Services
{
    internal sealed class AppController
    {
        public readonly SettingsStore SettingsStore;
        public readonly AccountStore Accounts;
        public readonly SecretVault Vault;
        public readonly ResultStore Results;
        public readonly SchedulerStateStore State;
        public readonly Scheduler Scheduler;

        public event Action AccountsChanged;
        public event Action ResultsChanged;
        public event Action<string> StatusText;
        public event Action<Account, CheckinOutcome> AccountCompleted;

        public AppController(SettingsStore settings, AccountStore accounts, SecretVault vault,
            ResultStore results, SchedulerStateStore state, Scheduler scheduler)
        {
            SettingsStore = settings;
            Accounts = accounts;
            Vault = vault;
            Results = results;
            State = state;
            Scheduler = scheduler;
            Scheduler.AccountCompleted += OnAccountCompleted;
            Scheduler.BatchCompleted += OnBatchCompleted;
            Scheduler.StatusChanged += OnStatus;
        }

        public Settings Settings { get { return SettingsStore.Current; } }

        public void Start()
        {
            AutostartService.SelfHeal();
            Scheduler.Start();
        }

        public List<AccountDraft> ScanAll()
        {
            return PlatformRegistry.ScanAll();
        }

        public List<AccountDraft> ScanPlatform(string platform)
        {
            ICheckinPlatform p = PlatformRegistry.Get(platform);
            if (p == null) return new List<AccountDraft>();
            return p.ScanLocal();
        }

        public AccountDraft ValidatePasted(string platform, string raw)
        {
            ICheckinPlatform p = PlatformRegistry.Get(platform);
            if (p == null) throw new Exception("未知平台");
            return p.ValidatePastedToken(raw);
        }

        public AccountDraft RunOAuth(string platform, Action<string, int> progress, Func<bool> cancelled)
        {
            ICheckinPlatform p = PlatformRegistry.Get(platform);
            if (p == null || !p.SupportsOAuth) throw new Exception("该平台不支持浏览器授权");
            WorkBuddyPlatform wb = p as WorkBuddyPlatform;
            if (wb != null) return wb.RunOAuth(progress, cancelled);
            QoderPlatform qoder = p as QoderPlatform;
            if (qoder != null) return qoder.RunOAuth(progress, cancelled);
            throw new Exception("该平台暂不支持浏览器授权");
        }

        public Account AddDraft(AccountDraft draft, out string error)
        {
            error = null;
            if (draft == null || draft.Secret == null)
            {
                error = draft != null && !string.IsNullOrEmpty(draft.Warning) ? draft.Warning : "账号凭证无效";
                return null;
            }
            Account a = Accounts.Upsert(draft, out error);
            if (a != null)
            {
                FireAccountsChanged();
                FireStatus("已添加账号：" + a.Name);
                try
                {
                    AccountRunState st = State.Get(a.Id);
                    if (SchedulerPolicy.ShouldRun(DateTime.Now, a, Settings, st))
                        Scheduler.RunNowAsync(false, a.Platform, a.Id, null);
                }
                catch { }
            }
            return a;
        }

        public void RemoveAccount(string id)
        {
            Accounts.Remove(id);
            FireAccountsChanged();
        }

        public void SetAccountEnabled(string id, bool enabled)
        {
            Accounts.SetEnabled(id, enabled);
            FireAccountsChanged();
        }

        public void RenameAccount(string id, string name)
        {
            Accounts.Rename(id, name);
            FireAccountsChanged();
        }

        public void RunNow(bool dryRun, string platformId, string accountId, Action<int, int> done)
        {
            Scheduler.RunNowAsync(dryRun, platformId, accountId, done);
        }

        public Dictionary<string, CheckinRecord> Today()
        {
            return Results.Day(DateTime.Now.ToString("yyyy-MM-dd"));
        }

        public CheckinRecord RecordOf(string accountId)
        {
            return Results.Get(DateTime.Now.ToString("yyyy-MM-dd"), accountId);
        }

        public List<CheckinRecord> LastDays(string accountId, int days)
        {
            DateTime end = DateTime.Now.Date;
            DateTime start = end.AddDays(-(days - 1));
            return Results.Range(accountId, start, end);
        }

        public void SetAutoCheckin(bool enabled)
        {
            Settings s = SettingsStore.Current;
            s.AutoCheckin = enabled;
            SettingsStore.Save();
            FireStatus(enabled ? "自动签到已开启" : "自动签到已暂停");
        }

        public void SetPlatformEnabled(string platform, bool enabled)
        {
            Settings s = SettingsStore.Current;
            s.Platform(platform).Enabled = enabled;
            SettingsStore.Save();
            FireStatus(PlatformIds.DisplayName(platform) + (enabled ? " 已启用" : " 已停用"));
        }

        public void SetPlatformTarget(string platform, string target)
        {
            Settings s = SettingsStore.Current;
            s.Platform(platform).Target = target;
            SettingsStore.Save();
        }

        public void SetTheme(string theme)
        {
            Settings s = SettingsStore.Current;
            s.Theme = theme;
            SettingsStore.Save();
        }

        public void SaveSettings()
        {
            SettingsStore.Save();
        }

        private void OnAccountCompleted(Account account, CheckinOutcome outcome)
        {
            FireResultsChanged();
            Action<Account, CheckinOutcome> h = AccountCompleted;
            if (h != null)
            {
                try { h(account, outcome); } catch { }
            }
        }

        private void OnBatchCompleted(int total, int ok)
        {
            FireResultsChanged();
            if (total > 0) FireStatus("本轮完成 " + ok + "/" + total);
        }

        private void OnStatus(string text)
        {
            FireStatus(text);
        }

        private void FireAccountsChanged()
        {
            Action h = AccountsChanged;
            if (h != null) { try { h(); } catch { } }
        }

        private void FireResultsChanged()
        {
            Action h = ResultsChanged;
            if (h != null) { try { h(); } catch { } }
        }

        private void FireStatus(string text)
        {
            Action<string> h = StatusText;
            if (h != null) { try { h(text); } catch { } }
        }
    }
}
