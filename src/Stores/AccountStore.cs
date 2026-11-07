using System;
using System.Collections.Generic;
using AiSign.Core;

namespace AiSign.Stores
{
    internal sealed class AccountStore
    {
        private readonly object _lock = new object();
        private readonly SecretVault _vault;
        private AccountsFile _file;

        public AccountStore(SecretVault vault)
        {
            _vault = vault;
            _file = JsonStore.Load<AccountsFile>(AppPaths.AccountsFile, NewFile);
            if (_file.Accounts == null) _file.Accounts = new List<Account>();
        }

        private static AccountsFile NewFile()
        {
            return new AccountsFile();
        }

        public List<Account> All()
        {
            lock (_lock) return new List<Account>(_file.Accounts);
        }

        public Account Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            lock (_lock)
            {
                for (int i = 0; i < _file.Accounts.Count; i++)
                    if (_file.Accounts[i].Id == id) return _file.Accounts[i];
            }
            return null;
        }

        public List<Account> ByPlatform(string platform)
        {
            List<Account> result = new List<Account>();
            lock (_lock)
            {
                for (int i = 0; i < _file.Accounts.Count; i++)
                    if (_file.Accounts[i].Platform == platform) result.Add(_file.Accounts[i]);
            }
            return result;
        }

        public int Count
        {
            get { lock (_lock) return _file.Accounts.Count; }
        }

        public Account Upsert(AccountDraft draft, out string error)
        {
            error = null;
            if (draft == null || string.IsNullOrEmpty(draft.Platform) || draft.Secret == null)
            {
                error = "账号数据不完整";
                return null;
            }

            string identity = draft.Uid;
            if (string.IsNullOrEmpty(identity)) identity = draft.Name;
            if (string.IsNullOrEmpty(identity)) identity = draft.Secret.Token;
            if (string.IsNullOrEmpty(identity)) identity = draft.Secret.RefreshToken;
            if (string.IsNullOrEmpty(identity))
            {
                error = "无法识别账号身份";
                return null;
            }

            string id = Hash.AccountId(draft.Platform, identity);
            draft.Secret.Platform = draft.Platform;
            if (string.IsNullOrEmpty(draft.Secret.Uid)) draft.Secret.Uid = draft.Uid;
            if (string.IsNullOrEmpty(draft.Secret.Name)) draft.Secret.Name = draft.Name;

            lock (_lock)
            {
                Account existing = null;
                for (int i = 0; i < _file.Accounts.Count; i++)
                {
                    Account a = _file.Accounts[i];
                    if (a.Id == id || (a.Platform == draft.Platform && !string.IsNullOrEmpty(a.Uid) && a.Uid == draft.Uid))
                    {
                        existing = a;
                        break;
                    }
                }

                bool isNew = existing == null;
                string targetId = isNew ? id : existing.Id;

                SecretPayload oldSecret = _vault.Get(targetId);
                if (oldSecret != null)
                {
                    if (draft.Secret.Uid == null || draft.Secret.Uid.Length == 0) draft.Secret.Uid = oldSecret.Uid;
                    if (draft.Secret.RefreshToken == null || draft.Secret.RefreshToken.Length == 0) draft.Secret.RefreshToken = oldSecret.RefreshToken;
                    if (draft.Secret.Pat == null || draft.Secret.Pat.Length == 0) draft.Secret.Pat = oldSecret.Pat;
                    if (draft.Secret.Platform == PlatformIds.Qoder && !string.IsNullOrEmpty(oldSecret.MachineId))
                        draft.Secret.MachineId = oldSecret.MachineId;
                }

                if (!_vault.Save(targetId, draft.Secret))
                {
                    error = "凭证加密保存失败（本机 DPAPI 不可用）";
                    return null;
                }

                if (isNew)
                {
                    if (draft.Platform == PlatformIds.Trae)
                    {
                        List<Account> old = new List<Account>();
                        for (int i = 0; i < _file.Accounts.Count; i++)
                            if (_file.Accounts[i].Platform == PlatformIds.Trae) old.Add(_file.Accounts[i]);
                        for (int i = 0; i < old.Count; i++)
                        {
                            _file.Accounts.Remove(old[i]);
                            _vault.Remove(old[i].Id);
                            Logger.Info("accounts", "Trae 单账号约束：替换旧账号 " + old[i].Id);
                        }
                    }

                    existing = new Account();
                    existing.Id = targetId;
                    existing.Platform = draft.Platform;
                    existing.AddedAt = Now();
                    existing.Sort = _file.Accounts.Count;
                    existing.Enabled = true;
                    _file.Accounts.Add(existing);
                }

                existing.Uid = draft.Uid;
                existing.Name = string.IsNullOrEmpty(draft.Name) ? "未命名账号" : draft.Name;
                existing.Source = draft.Source;
                existing.AuthState = AccountState.Ok;
                existing.VerifiedAt = Now();
                existing.CredentialUpdatedAt = Now();

                JsonStore.Save(AppPaths.AccountsFile, _file);
                return existing;
            }
        }

        public void Remove(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (_lock)
            {
                for (int i = _file.Accounts.Count - 1; i >= 0; i--)
                {
                    if (_file.Accounts[i].Id == id)
                    {
                        _file.Accounts.RemoveAt(i);
                        _vault.Remove(id);
                    }
                }
                JsonStore.Save(AppPaths.AccountsFile, _file);
            }
        }

        public void SetEnabled(string id, bool enabled)
        {
            lock (_lock)
            {
                Account a = FindLocked(id);
                if (a == null) return;
                a.Enabled = enabled;
                JsonStore.Save(AppPaths.AccountsFile, _file);
            }
        }

        public void SetAuthState(string id, string state)
        {
            lock (_lock)
            {
                Account a = FindLocked(id);
                if (a == null) return;
                a.AuthState = state;
                JsonStore.Save(AppPaths.AccountsFile, _file);
            }
        }

        public void Rename(string id, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            lock (_lock)
            {
                Account a = FindLocked(id);
                if (a == null) return;
                a.Name = name.Trim();
                JsonStore.Save(AppPaths.AccountsFile, _file);
            }
        }

        private Account FindLocked(string id)
        {
            for (int i = 0; i < _file.Accounts.Count; i++)
                if (_file.Accounts[i].Id == id) return _file.Accounts[i];
            return null;
        }

        private static string Now()
        {
            return DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
        }
    }
}
