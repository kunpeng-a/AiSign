using System;
using System.Collections.Generic;
using System.Text;

namespace AiSign.Core
{
    internal sealed class SecretVault
    {
        private readonly object _lock = new object();
        private SecretsFile _file;

        public SecretVault()
        {
            _file = JsonStore.Load<SecretsFile>(AppPaths.SecretsFile, NewFile);
            if (_file.Items == null) _file.Items = new Dictionary<string, SecretItem>();
        }

        private static SecretsFile NewFile()
        {
            return new SecretsFile();
        }

        public bool Has(string accountId)
        {
            if (string.IsNullOrEmpty(accountId)) return false;
            lock (_lock) return _file.Items.ContainsKey(accountId);
        }

        public SecretPayload Get(string accountId)
        {
            if (string.IsNullOrEmpty(accountId)) return null;
            SecretItem item;
            lock (_lock)
            {
                if (!_file.Items.TryGetValue(accountId, out item) || item == null) return null;
            }
            try
            {
                byte[] enc = Convert.FromBase64String(item.Blob);
                byte[] plain = Dpapi.Unprotect(enc);
                string json = Encoding.UTF8.GetString(plain);
                return JsonUtil.Deserialize<SecretPayload>(json);
            }
            catch
            {
                return null;
            }
        }

        public bool Save(string accountId, SecretPayload payload)
        {
            if (string.IsNullOrEmpty(accountId) || payload == null) return false;
            try
            {
                string json = JsonUtil.Serialize(payload);
                byte[] enc = Dpapi.Protect(Encoding.UTF8.GetBytes(json));
                SecretItem item = new SecretItem();
                item.UpdatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
                item.Blob = Convert.ToBase64String(enc);
                lock (_lock)
                {
                    _file.Items[accountId] = item;
                    JsonStore.Save(AppPaths.SecretsFile, _file);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("vault", "保存凭证失败: " + accountId, ex);
                return false;
            }
        }

        public void Remove(string accountId)
        {
            if (string.IsNullOrEmpty(accountId)) return;
            lock (_lock)
            {
                if (_file.Items.Remove(accountId))
                    JsonStore.Save(AppPaths.SecretsFile, _file);
            }
        }

        public List<string> Ids()
        {
            lock (_lock) return new List<string>(_file.Items.Keys);
        }
    }
}
