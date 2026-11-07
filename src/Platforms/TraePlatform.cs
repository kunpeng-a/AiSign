using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AiSign.Core;
using AiSign.Crypto;
using AiSign.Net;

namespace AiSign.Platforms
{
    internal sealed class TraePlatform : ICheckinPlatform
    {
        private const string StatusUrl = "https://api.trae.cn/trae/api/v2/ug/checkin_credits/status";
        private const string ClaimUrl = "https://api.trae.cn/trae/api/v2/ug/checkin_credits/claim";
        private static readonly string[] Brands = new string[] { "Trae CN", "TRAE SOLO CN", "TraeWork" };
        private static readonly Regex DcIdPattern = new Regex("^iCubeAuthInfo://icube-dc:(\\d+)", RegexOptions.Compiled);

        public string Id { get { return PlatformIds.Trae; } }
        public string DisplayName { get { return "Trae CN"; } }
        public bool SupportsMultiAccount { get { return false; } }
        public bool SupportsOAuth { get { return false; } }

        public List<AccountDraft> ScanLocal()
        {
            List<AccountDraft> result = new List<AccountDraft>();
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            List<string> seen = new List<string>();

            for (int i = 0; i < Brands.Length; i++)
            {
                string file = Path.Combine(root, Brands[i], "User", "globalStorage", "storage.json");
                AddIfAny(result, seen, Brands[i], file);
            }

            try
            {
                foreach (string dir in Directory.GetDirectories(root))
                {
                    string name = Path.GetFileName(dir);
                    if (string.Equals(name, "AiSign", StringComparison.OrdinalIgnoreCase)) continue;
                    bool known = false;
                    for (int i = 0; i < Brands.Length; i++)
                        if (string.Equals(name, Brands[i], StringComparison.OrdinalIgnoreCase)) { known = true; break; }
                    if (known) continue;
                    AddIfAny(result, seen, name, Path.Combine(dir, "User", "globalStorage", "storage.json"));
                }
            }
            catch { }
            return result;
        }

        private static void AddIfAny(List<AccountDraft> result, List<string> seen, string brand, string file)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;
            if (seen.Contains(file)) return;
            seen.Add(file);
            AccountDraft d = ReadAuth(brand, file);
            if (d != null) result.Add(d);
        }

        private static AccountDraft ReadAuth(string brand, string file)
        {
            try
            {
                Dictionary<string, object> root = ScanUtil.ReadJson(file);
                if (root == null) return null;

                string dcId = null;
                foreach (string key in root.Keys)
                {
                    Match m = DcIdPattern.Match(key);
                    if (m.Success) { dcId = m.Groups[1].Value; break; }
                }

                string enc = null;
                foreach (string key in root.Keys)
                {
                    if (key.StartsWith("iCubeAuthInfo://icube.cloudide", StringComparison.Ordinal))
                    {
                        enc = JsonUtil.GetString(root, key);
                        break;
                    }
                }
                if (string.IsNullOrEmpty(enc)) return null;
                if (string.IsNullOrEmpty(dcId))
                {
                    dcId = JsonUtil.GetString(root, "telemetry.devDeviceId");
                    if (string.IsNullOrEmpty(dcId)) dcId = JsonUtil.GetString(root, "telemetry.machineId");
                }

                string plain = TraeCipher.Decrypt(enc);
                Dictionary<string, object> parsed = JsonUtil.ParseObject(plain);
                if (parsed == null) return null;

                string token = JsonUtil.GetString(parsed, "token");
                if (string.IsNullOrEmpty(token)) return null;
                string username = null;
                Dictionary<string, object> account = JsonUtil.GetDict(parsed, "account");
                if (account != null) username = JsonUtil.GetString(account, "username");
                string region = null;
                Dictionary<string, object> userRegion = JsonUtil.GetDict(parsed, "userRegion");
                if (userRegion != null) region = JsonUtil.GetString(userRegion, "region");

                string identity = username;
                if (string.IsNullOrEmpty(identity)) identity = dcId;
                if (string.IsNullOrEmpty(identity)) identity = Hash.Sha256Hex(token);

                SecretPayload secret = new SecretPayload();
                secret.Platform = PlatformIds.Trae;
                secret.Token = token;
                secret.DeviceId = dcId;
                secret.Region = region;
                secret.Uid = identity;
                secret.Name = username;
                secret.ExpiresAt = JwtUtil.IsoToUnixMs(JsonUtil.GetString(parsed, "expiredAt"));
                secret.UpdatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

                AccountDraft d = new AccountDraft();
                d.Platform = PlatformIds.Trae;
                d.Uid = identity;
                d.Name = string.IsNullOrEmpty(username) ? brand : username;
                d.Source = "scan";
                d.Secret = secret;
                return d;
            }
            catch (Exception ex)
            {
                Logger.Warn("trae", "读取 " + brand + " 登录态失败: " + ex.Message);
                return null;
            }
        }

        public AccountDraft ValidatePastedToken(string raw)
        {
            if (string.IsNullOrEmpty(raw)) throw new Exception("请输入 Trae Token");
            string text = raw.Trim();
            if (text.StartsWith("Cloud-IDE-JWT ", StringComparison.OrdinalIgnoreCase))
                text = text.Substring("Cloud-IDE-JWT ".Length).Trim();

            string token = text;
            string username = null;
            string region = null;
            string deviceId = FindLocalDeviceId();

            if (TraeCipher.LooksLikeCipher(text))
            {
                string plain = TraeCipher.Decrypt(text);
                Dictionary<string, object> parsed = JsonUtil.ParseObject(plain);
                if (parsed == null) throw new Exception("iCube 密文解析失败");
                token = JsonUtil.GetString(parsed, "token");
                Dictionary<string, object> account = JsonUtil.GetDict(parsed, "account");
                if (account != null) username = JsonUtil.GetString(account, "username");
                Dictionary<string, object> userRegion = JsonUtil.GetDict(parsed, "userRegion");
                if (userRegion != null) region = JsonUtil.GetString(userRegion, "region");
            }

            if (string.IsNullOrEmpty(token)) throw new Exception("Token 为空");
            if (string.IsNullOrEmpty(username)) username = JwtUtil.Field(token, "username");
            if (string.IsNullOrEmpty(username)) username = JwtUtil.Sub(token);
            if (string.IsNullOrEmpty(deviceId)) deviceId = StableNumericId(token, 15);

            Dictionary<string, string> headers = BuildHeaders(token, deviceId, region);
            ApiResponse resp = HttpApi.Post(StatusUrl, headers, "{}", 25);
            if (resp.NetworkError) throw new Exception("网络异常：" + resp.Error);
            if (resp.Status == 401 || resp.Status == 403) throw new Exception("Token 已失效（HTTP " + resp.Status + "）");
            Dictionary<string, object> body = JsonUtil.ParseObject(resp.Body);
            if (body == null) throw new Exception("接口返回无法解析（HTTP " + resp.Status + "）");
            if (JsonUtil.GetNullableInt(body, "credits") == null && JsonUtil.GetNullableInt(body, "extra_credits") == null)
                throw new Exception("Token 无效或已过期");

            string identity = username;
            if (string.IsNullOrEmpty(identity)) identity = deviceId;
            if (string.IsNullOrEmpty(identity)) identity = Hash.Sha256Hex(token);

            SecretPayload secret = new SecretPayload();
            secret.Platform = PlatformIds.Trae;
            secret.Token = token;
            secret.DeviceId = deviceId;
            secret.Region = region;
            secret.Uid = identity;
            secret.Name = username;
            secret.UpdatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

            AccountDraft d = new AccountDraft();
            d.Platform = PlatformIds.Trae;
            d.Uid = identity;
            d.Name = string.IsNullOrEmpty(username) ? "Trae 账号" : username;
            d.Source = "token";
            d.Secret = secret;
            return d;
        }

        public CheckinOutcome Run(Account account, SecretPayload secret, Action<SecretPayload> persist, bool dryRun)
        {
            if (secret == null || string.IsNullOrEmpty(secret.Token))
                return CheckinOutcome.Make(OutcomeKind.AuthRequired, -1, "Trae Token 缺失，请重新添加账号");

            Dictionary<string, string> headers = BuildHeaders(secret.Token, secret.DeviceId, secret.Region);
            ApiResponse status = HttpApi.Post(StatusUrl, headers, "{}", 25);
            if (status.NetworkError)
                return CheckinOutcome.Make(OutcomeKind.Transient, -3, "网络异常：" + status.Error);
            if (status.Status == 401 || status.Status == 403)
                return CheckinOutcome.Make(OutcomeKind.AuthRequired, -6, "Trae 登录已失效，请重新登录客户端");

            Dictionary<string, object> body = JsonUtil.ParseObject(status.Body);
            if (body == null)
                return CheckinOutcome.Make(OutcomeKind.Transient, status.Status, "Trae 状态接口返回异常");

            int? baseCredits = JsonUtil.GetNullableInt(body, "credits");
            int? extraCredits = JsonUtil.GetNullableInt(body, "extra_credits");
            if (baseCredits == null && extraCredits == null)
                return CheckinOutcome.Make(OutcomeKind.AuthRequired, -6, "账号状态异常（Token 可能已过期）");

            int credits = (baseCredits.HasValue ? baseCredits.Value : 0) + (extraCredits.HasValue ? extraCredits.Value : 0);
            if (JsonUtil.GetBool(body, "checked_in"))
            {
                CheckinOutcome already = CheckinOutcome.Make(OutcomeKind.Already, 0, "今日已签到");
                already.Credits = credits;
                already.Confirmed = true;
                return already;
            }
            if (!JsonUtil.GetBool(body, "enable"))
                return CheckinOutcome.Make(OutcomeKind.Pending, 0, "Trae 签到未开启");
            if (dryRun)
            {
                CheckinOutcome dry = CheckinOutcome.Make(OutcomeKind.Pending, 0, "预检通过，今日可签到");
                dry.Credits = credits;
                return dry;
            }

            ApiResponse claim = HttpApi.Post(ClaimUrl, headers, "{\"req_source\":1}", 25);
            if (claim.NetworkError)
                return CheckinOutcome.Make(OutcomeKind.Transient, -3, "网络异常：" + claim.Error);
            if (claim.Status == 401 || claim.Status == 403)
                return CheckinOutcome.Make(OutcomeKind.AuthRequired, -6, "Trae 登录已失效，请重新登录客户端");

            Dictionary<string, object> claimBody = JsonUtil.ParseObject(claim.Body);
            int claimCode = claimBody == null ? claim.Status : JsonUtil.GetInt(claimBody, "code");
            bool claimOk = claimCode == 0 || JsonUtil.GetBool(claimBody, "checked_in");
            if (claimCode == 9095)
                return CheckinOutcome.Make(OutcomeKind.Occupied, 9095, "本设备今日已有账号签到");
            if (!claimOk)
            {
                string msg = claimBody == null ? "签到失败（HTTP " + claim.Status + "）" : JsonUtil.GetStringAny(claimBody, "message", "msg");
                return CheckinOutcome.Make(OutcomeKind.Transient, claimCode, string.IsNullOrEmpty(msg) ? "签到失败" : msg);
            }

            ApiResponse confirm = HttpApi.Post(StatusUrl, headers, "{}", 25);
            if (confirm.NetworkError)
            {
                CheckinOutcome unsure = CheckinOutcome.Make(OutcomeKind.SuccessUnconfirmed, 0, "已提交签到，结果待确认");
                unsure.Confirmed = false;
                return unsure;
            }
            Dictionary<string, object> confirmBody = JsonUtil.ParseObject(confirm.Body);
            bool checkedIn = confirmBody != null && JsonUtil.GetBool(confirmBody, "checked_in");
            if (checkedIn)
            {
                int c1 = JsonUtil.GetNullableInt(confirmBody, "credits") ?? 0;
                int c2 = JsonUtil.GetNullableInt(confirmBody, "extra_credits") ?? 0;
                CheckinOutcome ok = CheckinOutcome.Make(OutcomeKind.Success, 0, "签到成功");
                ok.Credits = c1 + c2;
                ok.Confirmed = true;
                return ok;
            }
            CheckinOutcome unconfirmed = CheckinOutcome.Make(OutcomeKind.SuccessUnconfirmed, 0, "已提交签到，但未确认到已签状态");
            unconfirmed.Confirmed = false;
            return unconfirmed;
        }

        public bool NeedsRefresh(SecretPayload secret) { return false; }
        public SecretPayload Refresh(SecretPayload secret) { return null; }

        private static Dictionary<string, string> BuildHeaders(string token, string deviceId, string region)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["Authorization"] = "Cloud-IDE-JWT " + token;
            headers["x-device-id"] = string.IsNullOrEmpty(deviceId) ? StableNumericId(token, 15) : deviceId;
            if (!string.IsNullOrEmpty(region)) headers["X-User-Region"] = region;
            return headers;
        }

        private static string FindLocalDeviceId()
        {
            try
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                for (int i = 0; i < Brands.Length; i++)
                {
                    string file = Path.Combine(root, Brands[i], "User", "globalStorage", "storage.json");
                    Dictionary<string, object> d = ScanUtil.ReadJson(file);
                    if (d == null) continue;
                    foreach (string key in d.Keys)
                    {
                        Match m = DcIdPattern.Match(key);
                        if (m.Success) return m.Groups[1].Value;
                    }
                    string dev = JsonUtil.GetString(d, "telemetry.devDeviceId");
                    if (!string.IsNullOrEmpty(dev)) return dev;
                }
            }
            catch { }
            return null;
        }

        private static string StableNumericId(string seed, int length)
        {
            string hex = Hash.Sha256Hex(seed == null ? "trae" : seed);
            StringBuilder sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                int v = Convert.ToInt32(hex.Substring(i % 32, 1), 16);
                sb.Append((char)('0' + (v % 10)));
            }
            return sb.ToString();
        }
    }
}
