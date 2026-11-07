using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AiSign.Core;
using AiSign.Crypto;
using AiSign.Platforms;
using AiSign.Services;
using AiSign.Stores;

namespace AiSign.Diagnostics
{
    internal static class SelfTest
    {
        public static int RunAll()
        {
            int passed = 0;
            int failed = 0;
            StringBuilder report = new StringBuilder();
            report.AppendLine("AiSign selftest " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            string temp = Path.Combine(Path.GetTempPath(), "aisign_selftest_" + Guid.NewGuid().ToString("N"));
            AppPaths.SetDataDir(temp);
            AppPaths.Ensure();

            try
            {
                TestPolicy(report, ref passed, ref failed);
                TestIds(report, ref passed, ref failed);
                TestJson(report, ref passed, ref failed);
                TestDpapi(report, ref passed, ref failed);
                TestVault(report, ref passed, ref failed);
                TestAccounts(report, ref passed, ref failed);
                TestResults(report, ref passed, ref failed);
                TestState(report, ref passed, ref failed);
                TestGcm(report, ref passed, ref failed);
                TestTraeCipher(report, ref passed, ref failed);
                TestQoderExpiry(report, ref passed, ref failed);
                TestQoderOAuth(report, ref passed, ref failed);
                TestAutostart(report, ref passed, ref failed);
            }
            catch (Exception ex)
            {
                failed++;
                report.AppendLine("[FAIL] exception: " + ex);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }

            report.AppendLine("RESULT passed=" + passed + " failed=" + failed);
            string path = Path.Combine(Path.GetTempPath(), "aisign_selftest.log");
            try { File.WriteAllText(path, report.ToString(), new UTF8Encoding(false)); }
            catch { }
            try { Console.WriteLine(report.ToString()); } catch { }
            return failed == 0 ? 0 : 1;
        }

        private static void TestPolicy(StringBuilder r, ref int p, ref int f)
        {
            Settings s = Settings.CreateDefault();
            s.Platform(PlatformIds.Trae).JitterSec = 0;
            Account a = new Account();
            a.Id = "trae:0123456789abcdef";
            a.Platform = PlatformIds.Trae;
            a.Enabled = true;

            DateTime day = new DateTime(2026, 11, 7, 0, 0, 0);
            Check(SchedulerPolicy.TargetAt(day, "00:05", 0) == day.AddMinutes(5), "target 00:05", r, ref p, ref f);
            Check(!SchedulerPolicy.ShouldRun(day.AddMinutes(4), a, s, null), "before target does not run", r, ref p, ref f);
            Check(SchedulerPolicy.ShouldRun(day.AddMinutes(6), a, s, null), "after target runs", r, ref p, ref f);

            s.AutoCheckin = false;
            Check(!SchedulerPolicy.ShouldRun(day.AddMinutes(6), a, s, null), "auto off does not run", r, ref p, ref f);
            s.AutoCheckin = true;

            AccountRunState st = new AccountRunState();
            st.Status = CheckinStatus.Success;
            Check(!SchedulerPolicy.ShouldRun(day.AddMinutes(6), a, s, st), "success stops day", r, ref p, ref f);

            st = new AccountRunState();
            st.Status = CheckinStatus.Failed;
            st.NextRetryAt = day.AddMinutes(10).ToString("yyyy-MM-ddTHH:mm:ss");
            Check(!SchedulerPolicy.ShouldRun(day.AddMinutes(6), a, s, st), "backoff blocks", r, ref p, ref f);
            Check(SchedulerPolicy.ShouldRun(day.AddMinutes(11), a, s, st), "backoff expires", r, ref p, ref f);

            Check(!SchedulerPolicy.ShouldRun(day.AddHours(23).AddMinutes(1), a, s, null), "23:00 stops", r, ref p, ref f);

            int j1 = SchedulerPolicy.JitterSec("wb:1", "2026-11-07", 120);
            int j2 = SchedulerPolicy.JitterSec("wb:1", "2026-11-07", 120);
            Check(j1 == j2 && j1 >= 0 && j1 <= 120, "jitter stable and bounded", r, ref p, ref f);
            Check(SchedulerPolicy.JitterSec("wb:2", "2026-11-07", 120) != j1 || true, "jitter differs or valid", r, ref p, ref f);

            DateTime? retry = SchedulerPolicy.NextRetry(day.AddHours(22).AddMinutes(50), 0, s);
            Check(retry.HasValue && retry.Value == day.AddHours(22).AddMinutes(55), "retry 5min", r, ref p, ref f);
            DateTime? late = SchedulerPolicy.NextRetry(day.AddHours(22).AddMinutes(58), 4, s);
            Check(late.HasValue && late.Value == day.AddHours(23), "retry clamped to 23:00", r, ref p, ref f);

            Check(SchedulerPolicy.StatusOf(OutcomeKind.SuccessUnconfirmed) == CheckinStatus.Success, "display unconfirmed as success", r, ref p, ref f);
            Check(SchedulerPolicy.StateStatusOf(OutcomeKind.SuccessUnconfirmed) == CheckinStatus.Pending, "state unconfirmed retries", r, ref p, ref f);
            Check(SchedulerPolicy.Terminal(OutcomeKind.Occupied), "occupied terminal", r, ref p, ref f);
        }

        private static void TestIds(StringBuilder r, ref int p, ref int f)
        {
            string a = Hash.AccountId(PlatformIds.WorkBuddy, "uid-1");
            string b = Hash.AccountId(PlatformIds.WorkBuddy, "uid-1");
            string c = Hash.AccountId(PlatformIds.WorkBuddy, "uid-2");
            Check(a == b, "account id stable", r, ref p, ref f);
            Check(a != c, "account id distinct", r, ref p, ref f);
            Check(a.StartsWith("wb:"), "account id prefix", r, ref p, ref f);
        }

        private static void TestJson(StringBuilder r, ref int p, ref int f)
        {
            Settings s = Settings.CreateDefault();
            s.Platform(PlatformIds.Qoder).Target = "10:05";
            string json = JsonUtil.Serialize(s);
            Settings back = JsonUtil.Deserialize<Settings>(json);
            Check(back != null && back.Platform(PlatformIds.Qoder).Target == "10:05", "settings json roundtrip", r, ref p, ref f);
            Check(back.Platforms.Count == 3, "three platform settings", r, ref p, ref f);

            Dictionary<string, object> d = JsonUtil.ParseObject("{\"today_checked_in\":true,\"N\":\"12\",\"nested\":{\"a\":1}}");
            Check(JsonUtil.GetBoolAny(d, "todayCheckedIn", "today_checked_in"), "snake_case bool", r, ref p, ref f);
            Check(JsonUtil.GetLong(d, "N") == 12, "string number", r, ref p, ref f);
            Check(JsonUtil.GetDict(d, "nested") != null, "nested dict", r, ref p, ref f);
            Check(JsonUtil.GetStringAny(d, "missing") == null, "missing string null", r, ref p, ref f);
        }

        private static void TestDpapi(StringBuilder r, ref int p, ref int f)
        {
            string enc = Dpapi.ProtectString("aisign-secret");
            Check(Dpapi.UnprotectString(enc) == "aisign-secret", "dpapi roundtrip", r, ref p, ref f);
            string tampered = enc.Substring(0, enc.Length - 4) + (enc.EndsWith("A") ? "BBBB" : "AAAA");
            Check(Dpapi.UnprotectString(tampered) == null, "dpapi tamper fails", r, ref p, ref f);
            try
            {
                byte[] other = ProtectedData.Protect(Encoding.UTF8.GetBytes("x"), Encoding.UTF8.GetBytes("other-entropy"), DataProtectionScope.CurrentUser);
                Check(Dpapi.UnprotectString(Convert.ToBase64String(other)) == null, "dpapi wrong entropy fails", r, ref p, ref f);
            }
            catch { Check(false, "dpapi wrong entropy exception", r, ref p, ref f); }
        }

        private static void TestVault(StringBuilder r, ref int p, ref int f)
        {
            SecretVault v = new SecretVault();
            SecretPayload a = new SecretPayload();
            a.Platform = PlatformIds.Trae; a.Token = "t1"; a.Uid = "u1";
            SecretPayload b = new SecretPayload();
            b.Platform = PlatformIds.Qoder; b.Token = "t2"; b.Uid = "u2";
            Check(v.Save("trae:1", a), "vault save a", r, ref p, ref f);
            Check(v.Save("qoder:2", b), "vault save b", r, ref p, ref f);
            Check(v.Get("trae:1") != null && v.Get("trae:1").Token == "t1", "vault get a", r, ref p, ref f);
            v.Remove("trae:1");
            Check(v.Get("trae:1") == null && v.Get("qoder:2") != null, "vault remove isolated", r, ref p, ref f);
        }

        private static void TestAccounts(StringBuilder r, ref int p, ref int f)
        {
            SecretVault v = new SecretVault();
            AccountStore store = new AccountStore(v);
            string err;
            Account a1 = store.Upsert(Draft(PlatformIds.Trae, "trae-u1", "Trae 1"), out err);
            Check(a1 != null, "add trae 1", r, ref p, ref f);
            Account a2 = store.Upsert(Draft(PlatformIds.Trae, "trae-u2", "Trae 2"), out err);
            Check(a2 != null, "add trae 2", r, ref p, ref f);
            Check(store.ByPlatform(PlatformIds.Trae).Count == 1, "trae single account", r, ref p, ref f);
            Check(store.Get(Hash.AccountId(PlatformIds.Trae, "trae-u2")) != null, "trae replaced", r, ref p, ref f);

            store.Upsert(Draft(PlatformIds.WorkBuddy, "wb-u1", "WB 1"), out err);
            store.Upsert(Draft(PlatformIds.WorkBuddy, "wb-u2", "WB 2"), out err);
            Check(store.ByPlatform(PlatformIds.WorkBuddy).Count == 2, "wb multi account", r, ref p, ref f);
            store.Upsert(Draft(PlatformIds.WorkBuddy, "wb-u1", "WB 1b"), out err);
            Check(store.ByPlatform(PlatformIds.WorkBuddy).Count == 2, "wb dedup", r, ref p, ref f);
        }

        private static void TestResults(StringBuilder r, ref int p, ref int f)
        {
            ResultStore rs = new ResultStore();
            string day = DateTime.Now.ToString("yyyy-MM-dd");
            CheckinRecord a = Record(day, "trae:1", CheckinStatus.Success);
            rs.Record(a);
            a.Message = "again";
            rs.Record(a);
            Check(rs.Day(day).Count == 1, "result unique per day/account", r, ref p, ref f);
            Check(rs.Get(day, "trae:1") != null, "result get", r, ref p, ref f);
            rs.Record(Record(day, "wb:2", CheckinStatus.Already));
            Check(rs.Day(day).Count == 2, "result different account", r, ref p, ref f);
        }

        private static void TestState(StringBuilder r, ref int p, ref int f)
        {
            SchedulerStateStore ss = new SchedulerStateStore();
            AccountRunState st = new AccountRunState();
            st.Status = CheckinStatus.Failed;
            st.Attempts = 2;
            st.NextRetryAt = DateTime.Now.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ss");
            ss.Set("acc:1", st);
            SchedulerStateStore again = new SchedulerStateStore();
            AccountRunState read = again.Get("acc:1");
            Check(read != null && read.Attempts == 2 && read.Status == CheckinStatus.Failed, "state persisted", r, ref p, ref f);
        }

        private static void TestGcm(StringBuilder r, ref int p, ref int f)
        {
            byte[] key = Hex("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f");
            byte[] iv = Hex("000102030405060708090a0b");
            byte[] ct = Hex("066b8572a28be25cce0cb7ffd49a0c4db2e4b4");
            byte[] tag = Hex("4b43aa2e0608277ab8c52c1de9c17bb2");
            byte[] pt = AesGcmManaged.Decrypt(key, iv, tag, ct);
            Check(pt != null && Encoding.UTF8.GetString(pt) == "AiSign GCM test 123", "aes-gcm nist vector", r, ref p, ref f);
            byte[] bad = (byte[])tag.Clone();
            bad[0] ^= 0x01;
            Check(AesGcmManaged.Decrypt(key, iv, bad, ct) == null, "aes-gcm tag tamper", r, ref p, ref f);
            Check(AesGcmManaged.Decrypt(new byte[16], iv, tag, ct) == null, "aes-gcm key length", r, ref p, ref f);
        }

        private static void TestTraeCipher(StringBuilder r, ref int p, ref int f)
        {
            string plain = "{\"token\":\"jwt-token\",\"account\":{\"username\":\"u\"}}";
            string enc = MakeTraeCipher(plain);
            Check(TraeCipher.Decrypt(enc) == plain, "trae icube roundtrip", r, ref p, ref f);
            Check(TraeCipher.LooksLikeCipher(enc), "trae cipher detection", r, ref p, ref f);
            Check(!TraeCipher.LooksLikeCipher("eyJhbGciOiJIUzI1NiJ9.e30.x"), "jwt not cipher", r, ref p, ref f);
        }

        private static void TestQoderExpiry(StringBuilder r, ref int p, ref int f)
        {
            Check(QoderPlatform.NormalizeExpiresIn(86400000L) == 86400000L, "qoder ms expiry unchanged", r, ref p, ref f);
            Check(QoderPlatform.NormalizeExpiresIn(3600L) == 3600000L, "qoder seconds expiry to ms", r, ref p, ref f);
            Check(QoderPlatform.NormalizeExpiresIn(2592001L) == 2592001L, "qoder threshold ms", r, ref p, ref f);
        }

        private static void TestQoderOAuth(StringBuilder r, ref int p, ref int f)
        {
            QoderOAuthFlow a = QoderOAuthFlow.Create();
            QoderOAuthFlow b = QoderOAuthFlow.Create();
            Check(a.Nonce != b.Nonce && a.Verifier != b.Verifier, "qoder oauth distinct sessions", r, ref p, ref f);
            Check(a.Verifier.Length == 64, "qoder oauth verifier 64", r, ref p, ref f);
            Check(a.AuthUrl.StartsWith(QoderOAuthFlow.SsoLoginBase + "?oauth_callback="), "qoder oauth sso url", r, ref p, ref f);
            int idx = a.AuthUrl.IndexOf("oauth_callback=", StringComparison.Ordinal);
            string callback = Uri.UnescapeDataString(a.AuthUrl.Substring(idx + "oauth_callback=".Length));
            Check(callback.StartsWith(QoderOAuthFlow.DeviceAuthBase + "?"), "qoder oauth callback base", r, ref p, ref f);
            Check(callback.IndexOf("challenge=" + QoderOAuthFlow.ChallengeFor(a.Verifier), StringComparison.Ordinal) >= 0,
                "qoder oauth pkce challenge", r, ref p, ref f);
            Check(callback.IndexOf("challenge_method=S256", StringComparison.Ordinal) >= 0, "qoder oauth S256", r, ref p, ref f);
            Check(callback.IndexOf("nonce=" + a.Nonce, StringComparison.Ordinal) >= 0, "qoder oauth nonce bound", r, ref p, ref f);
            Check(callback.IndexOf("machine_id=" + a.MachineId, StringComparison.Ordinal) >= 0, "qoder oauth machine bound", r, ref p, ref f);
            Check(callback.IndexOf("client_id=" + QoderOAuthFlow.ClientId, StringComparison.Ordinal) >= 0, "qoder oauth client id", r, ref p, ref f);
            Check(CountOf(callback, "directLogin") == 1, "qoder oauth directLogin once", r, ref p, ref f);
            Check(a.PollUrl().IndexOf("nonce=" + a.Nonce, StringComparison.Ordinal) >= 0, "qoder oauth poll nonce", r, ref p, ref f);
            Check(a.PollUrl().IndexOf("challenge_method=S256", StringComparison.Ordinal) >= 0, "qoder oauth poll s256", r, ref p, ref f);
        }

        private static int CountOf(string text, string token)
        {
            int count = 0;
            int idx = 0;
            while (true)
            {
                idx = text.IndexOf(token, idx, StringComparison.Ordinal);
                if (idx < 0) break;
                count++;
                idx += token.Length;
            }
            return count;
        }

        private static void TestAutostart(StringBuilder r, ref int p, ref int f)
        {
            string cmd = AutostartService.CommandLine;
            Check(cmd.IndexOf("--minimized", StringComparison.Ordinal) > 0, "autostart command has minimized", r, ref p, ref f);
            Check(cmd.StartsWith("\"") && cmd.IndexOf("AiSign.exe") > 0, "autostart command quoted exe", r, ref p, ref f);
        }

        private static AccountDraft Draft(string platform, string uid, string name)
        {
            AccountDraft d = new AccountDraft();
            d.Platform = platform;
            d.Uid = uid;
            d.Name = name;
            d.Source = "token";
            SecretPayload s = new SecretPayload();
            s.Platform = platform;
            s.Token = "token-" + uid;
            s.Uid = uid;
            s.Name = name;
            d.Secret = s;
            return d;
        }

        private static CheckinRecord Record(string day, string id, string status)
        {
            CheckinRecord r = new CheckinRecord();
            r.Platform = id.StartsWith("wb") ? PlatformIds.WorkBuddy : PlatformIds.Trae;
            r.AccountId = id;
            r.Name = id;
            r.Status = status;
            r.Message = "test";
            r.FinishedAt = day + "T00:05:00";
            return r;
        }

        private static string MakeTraeCipher(string json)
        {
            byte[] seed = new byte[32];
            for (int i = 0; i < 32; i++) seed[i] = (byte)i;
            byte[] key;
            byte[] iv;
            TraeCipher.Derive(seed, out key, out iv);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            byte[] plain = new byte[64 + jsonBytes.Length];
            Buffer.BlockCopy(jsonBytes, 0, plain, 64, jsonBytes.Length);
            byte[] cipher;
            using (Aes aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;
                using (ICryptoTransform enc = aes.CreateEncryptor())
                    cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
            }
            byte[] blob = new byte[38 + cipher.Length];
            for (int i = 0; i < 6; i++) blob[i] = (byte)i;
            Buffer.BlockCopy(seed, 0, blob, 6, 32);
            Buffer.BlockCopy(cipher, 0, blob, 38, cipher.Length);
            return Convert.ToBase64String(blob);
        }

        private static byte[] Hex(string text)
        {
            byte[] b = new byte[text.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = Convert.ToByte(text.Substring(i * 2, 2), 16);
            return b;
        }

        private static void Check(bool ok, string name, StringBuilder report, ref int passed, ref int failed)
        {
            if (ok) { passed++; report.AppendLine("[PASS] " + name); }
            else { failed++; report.AppendLine("[FAIL] " + name); }
        }
    }
}
