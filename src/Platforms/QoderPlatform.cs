using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using AiSign.Core;
using AiSign.Crypto;
using AiSign.Net;

namespace AiSign.Platforms
{
    internal sealed class QoderOAuthFlow
    {
        public const string ClientId = "732aef47-9cf2-46a2-95fe-4cebb5d0d1fa";
        public const string DeviceAuthBase = "https://qoder.cn/device/selectAccounts";
        public const string SsoLoginBase = "https://qoder.cn/sso/login/aliyun";
        public const string PollBase = "https://openapi.qoder.com.cn/api/v1/deviceToken/poll";

        public string Nonce;
        public string Verifier;
        public string MachineId;
        public string Challenge;
        public string DeviceUrl;
        public string AuthUrl;

        public static QoderOAuthFlow Create()
        {
            QoderOAuthFlow f = new QoderOAuthFlow();
            f.Verifier = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            f.Nonce = Guid.NewGuid().ToString();
            f.MachineId = Guid.NewGuid().ToString();
            f.Challenge = ChallengeFor(f.Verifier);
            f.DeviceUrl = DeviceAuthBase +
                "?challenge=" + f.Challenge +
                "&challenge_method=S256" +
                "&nonce=" + f.Nonce +
                "&machine_id=" + f.MachineId +
                "&directLogin=true" +
                "&client_id=" + ClientId;
            f.AuthUrl = SsoLoginBase + "?oauth_callback=" + Uri.EscapeDataString(f.DeviceUrl);
            return f;
        }

        public string PollUrl()
        {
            return PollBase + "?nonce=" + Uri.EscapeDataString(Nonce) +
                "&verifier=" + Uri.EscapeDataString(Verifier) +
                "&challenge_method=S256";
        }

        public static string ChallengeFor(string verifier)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return Base64Url(sha.ComputeHash(Encoding.UTF8.GetBytes(verifier)));
            }
        }

        public static string Base64Url(byte[] data)
        {
            return Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }
    }

    internal sealed class QoderCampaign
    {
        public string CampaignId;
        public string ActionType;
        public string ClaimStatus;
        public int Amount;
        public long StartAt;
        public long EndAt;
    }

    internal sealed class QoderPlatform : ICheckinPlatform
    {
        private const string OpenApi = "https://openapi.qoder.com.cn";
        private const string WebBase = "https://qoder.cn";
        private const string CampaignsPath = "/sash/api/v1/me/campaigns?forceRefresh=true";
        private const string UserInfoPath = "/api/v1/userinfo";
        private const string JobTokenPath = "/api/v1/me/jobToken";
        private const string DeviceRefreshPath = "/api/v1/deviceToken/refresh";
        private const string JobRefreshPath = "/api/v1/jobToken/refresh";
        private const string CosyVersion = "0.4.8";

        private static readonly string[] KnownDirs = new string[]
        {
            "com.qodercn.app.stable", "com.qodercn.app", "com.qoder.app.stable", "com.qoder.app", "Qoder CN", "Qoder"
        };

        public string Id { get { return PlatformIds.Qoder; } }
        public string DisplayName { get { return "Qoder CN"; } }
        public bool SupportsMultiAccount { get { return true; } }
        public bool SupportsOAuth { get { return true; } }

        public List<AccountDraft> ScanLocal()
        {
            List<AccountDraft> result = new List<AccountDraft>();
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            List<string> dirs = new List<string>();

            for (int i = 0; i < KnownDirs.Length; i++)
            {
                string dir = Path.Combine(appData, KnownDirs[i]);
                if (File.Exists(Path.Combine(dir, "auth.v1.dat"))) dirs.Add(dir);
            }
            try
            {
                foreach (string dir in Directory.GetDirectories(appData))
                {
                    string name = Path.GetFileName(dir);
                    if (name.IndexOf("qoder", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (File.Exists(Path.Combine(dir, "auth.v1.dat"))) dirs.Add(dir);
                }
            }
            catch { }

            List<string> seen = new List<string>();
            for (int i = 0; i < dirs.Count; i++)
            {
                if (seen.Contains(dirs[i])) continue;
                seen.Add(dirs[i]);
                AccountDraft d = ReadClient(dirs[i]);
                if (d != null) result.Add(d);
            }
            return result;
        }

        private static AccountDraft ReadClient(string dir)
        {
            try
            {
                string statePath = Path.Combine(dir, "Local State");
                string authPath = Path.Combine(dir, "auth.v1.dat");
                Dictionary<string, object> state = ScanUtil.ReadJson(statePath);
                if (state == null) return null;
                Dictionary<string, object> osCrypt = JsonUtil.GetDict(state, "os_crypt");
                string encKey = osCrypt == null ? null : JsonUtil.GetString(osCrypt, "encrypted_key");
                if (string.IsNullOrEmpty(encKey)) return null;

                byte[] wrapped = Convert.FromBase64String(encKey);
                if (wrapped.Length <= 5) return null;
                if (wrapped[0] != (byte)'D' || wrapped[1] != (byte)'P' || wrapped[2] != (byte)'A'
                    || wrapped[3] != (byte)'P' || wrapped[4] != (byte)'I') return null;
                byte[] keyBlob = new byte[wrapped.Length - 5];
                Buffer.BlockCopy(wrapped, 5, keyBlob, 0, keyBlob.Length);
                byte[] key = ProtectedData.Unprotect(keyBlob, null, DataProtectionScope.CurrentUser);
                if (key == null || key.Length != 32) return null;

                byte[] blob = File.ReadAllBytes(authPath);
                if (blob.Length < 3 + 12 + 16) return null;
                if (blob[0] != (byte)'v' || blob[1] != (byte)'1' || blob[2] != (byte)'0') return null;
                int ctLen = blob.Length - 15 - 16;
                byte[] nonce = new byte[12];
                Buffer.BlockCopy(blob, 3, nonce, 0, 12);
                byte[] cipher = new byte[ctLen];
                if (ctLen > 0) Buffer.BlockCopy(blob, 15, cipher, 0, ctLen);
                byte[] tag = new byte[16];
                Buffer.BlockCopy(blob, blob.Length - 16, tag, 0, 16);

                byte[] plain = AesGcmManaged.Decrypt(key, nonce, tag, cipher);
                if (plain == null) return null;
                Dictionary<string, object> root = JsonUtil.ParseObject(Encoding.UTF8.GetString(plain));
                if (root == null) return null;
                string token = JsonUtil.GetString(root, "token");
                if (string.IsNullOrEmpty(token)) return null;

                string uid = null;
                string userName = null;
                Dictionary<string, object> user = JsonUtil.GetDict(root, "user");
                if (user != null)
                {
                    uid = JsonUtil.GetString(user, "id");
                    userName = JsonUtil.GetString(user, "name");
                }

                string identity = uid;
                if (string.IsNullOrEmpty(identity)) identity = Hash.Sha256Hex(token);

                SecretPayload secret = new SecretPayload();
                secret.Platform = PlatformIds.Qoder;
                secret.Token = token;
                secret.RefreshToken = JsonUtil.GetString(root, "refreshToken");
                secret.Uid = identity;
                secret.Name = userName;
                secret.ExpiresAt = JwtUtil.IsoToUnixMs(JsonUtil.GetString(root, "expiresAt"));
                secret.RefreshExpiresAt = JwtUtil.IsoToUnixMs(JsonUtil.GetString(root, "refreshTokenExpiresAt"));
                secret.MachineId = NewMachineId();
                secret.Kind = "device";
                secret.UpdatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

                AccountDraft d = new AccountDraft();
                d.Platform = PlatformIds.Qoder;
                d.Uid = identity;
                d.Name = string.IsNullOrEmpty(userName) ? "Qoder " + Hash.Mask(identity, 4, 4) : userName;
                d.Source = "scan";
                d.Secret = secret;
                return d;
            }
            catch (Exception ex)
            {
                Logger.Warn("qoder", "读取 auth.v1.dat 失败: " + ex.Message);
                return null;
            }
        }

        public AccountDraft ValidatePastedToken(string raw)
        {
            if (string.IsNullOrEmpty(raw)) throw new Exception("请输入 Qoder Token 或 PAT");
            string text = raw.Trim();
            string token = text;
            string refresh = null;
            string pat = null;
            string machineId = null;
            string uid = null;
            string name = null;

            if (text.StartsWith("{"))
            {
                Dictionary<string, object> root = JsonUtil.ParseObject(text);
                if (root == null) throw new Exception("JSON 无法解析");
                token = JsonUtil.GetStringAny(root, "token", "accessToken", "access_token");
                refresh = JsonUtil.GetStringAny(root, "refreshToken", "refresh_token");
                pat = JsonUtil.GetStringAny(root, "pat", "patToken");
                machineId = JsonUtil.GetStringAny(root, "machineId", "machine_id");
                uid = JsonUtil.GetStringAny(root, "uid", "userId", "user_id");
                name = JsonUtil.GetStringAny(root, "name", "nickname", "userName");
                if (string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(pat)) token = pat;
            }

            if (string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(refresh))
            {
                SecretPayload r = RefreshWithToken(refresh, null, null);
                if (r == null) throw new Exception("Refresh Token 换取 Access Token 失败");
                token = r.Token;
                refresh = r.RefreshToken;
            }
            if (string.IsNullOrEmpty(token)) throw new Exception("Token 为空");

            if (token.StartsWith("pt-", StringComparison.Ordinal))
            {
                pat = token;
                SecretPayload job = ExchangePat(token);
                if (job == null) throw new Exception("PAT 换取作业令牌失败");
                token = job.Token;
                refresh = job.RefreshToken;
            }
            else if (token.StartsWith("jrt-", StringComparison.Ordinal))
            {
                SecretPayload r = RefreshWithToken(token, null, null);
                if (r == null) throw new Exception("作业刷新令牌换取 Access Token 失败");
                token = r.Token;
                refresh = r.RefreshToken;
            }

            string resolvedMachineId = string.IsNullOrEmpty(machineId) ? NewMachineId() : machineId;
            Dictionary<string, string> headers = BuildHeaders(token, resolvedMachineId);
            FetchUserInfo(headers, out uid, out name);

            ApiResponse list = HttpApi.Get(OpenApi + CampaignsPath, headers, 25);
            if (list.NetworkError) throw new Exception("网络异常：" + list.Error);
            if (list.Status == 401 || list.Status == 403)
                throw new Exception("Qoder Token 已失效（HTTP " + list.Status + "）");
            if (!list.Ok && list.Status != 429)
                throw new Exception("Qoder 接口返回 HTTP " + list.Status);

            if (string.IsNullOrEmpty(uid)) uid = Hash.Sha256Hex(token);

            SecretPayload secret = new SecretPayload();
            secret.Platform = PlatformIds.Qoder;
            secret.Token = token;
            secret.RefreshToken = refresh;
            secret.Pat = pat;
            secret.Uid = uid;
            secret.Name = name;
            secret.MachineId = resolvedMachineId;
            secret.Kind = string.IsNullOrEmpty(pat) ? "device" : "pat";
            secret.UpdatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

            AccountDraft d = new AccountDraft();
            d.Platform = PlatformIds.Qoder;
            d.Uid = uid;
            d.Name = string.IsNullOrEmpty(name) ? "Qoder " + Hash.Mask(uid, 4, 4) : name;
            d.Source = "token";
            d.Secret = secret;
            return d;
        }

        public AccountDraft RunOAuth(Action<string, int> progress, Func<bool> cancelled)
        {
            QoderOAuthFlow flow = QoderOAuthFlow.Create();
            BrowserLauncher.OpenInPrivate(flow.AuthUrl);
            if (progress != null) progress("请在浏览器中完成 Qoder 登录与授权（3 分钟内有效）", 0);

            for (int i = 0; i < 180; i++)
            {
                if (cancelled != null && cancelled()) throw new Exception("已取消授权");
                Thread.Sleep(1000);
                if (cancelled != null && cancelled()) throw new Exception("已取消授权");
                if (i % 3 == 0 && progress != null) progress("等待浏览器授权… 剩余 " + (180 - i) + " 秒", i);

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers["User-Agent"] = "Qoder";
                headers["Accept"] = "application/json";
                ApiResponse poll = HttpApi.Get(flow.PollUrl(), headers, 20);
                if (poll.NetworkError)
                {
                    if (progress != null) progress("网络异常，正在重试…", i);
                    continue;
                }
                if (poll.Status == 404) continue; // pending
                if (poll.Status != 200) continue;

                Dictionary<string, object> root = JsonUtil.ParseObject(poll.Body);
                if (root == null) continue;
                Dictionary<string, object> data = JsonUtil.GetDict(root, "data");
                string respNonce = JsonUtil.GetString(root, "nonce");
                string token = JsonUtil.GetStringAny(root, "token", "accessToken", "access_token");
                string uid = JsonUtil.GetStringAny(root, "user_id", "userId", "uid");
                string refresh = JsonUtil.GetStringAny(root, "refresh_token", "refreshToken");
                long expiresIn = JsonUtil.GetLong(root, "expires_in");
                long refreshExpiresIn = JsonUtil.GetLong(root, "refresh_token_expires_in");
                if (data != null)
                {
                    if (string.IsNullOrEmpty(respNonce)) respNonce = JsonUtil.GetString(data, "nonce");
                    if (string.IsNullOrEmpty(token)) token = JsonUtil.GetStringAny(data, "token", "accessToken", "access_token");
                    if (string.IsNullOrEmpty(uid)) uid = JsonUtil.GetStringAny(data, "user_id", "userId", "uid");
                    if (string.IsNullOrEmpty(refresh)) refresh = JsonUtil.GetStringAny(data, "refresh_token", "refreshToken");
                    if (expiresIn == 0) expiresIn = JsonUtil.GetLong(data, "expires_in");
                    if (refreshExpiresIn == 0) refreshExpiresIn = JsonUtil.GetLong(data, "refresh_token_expires_in");
                }
                // nonce 回验：拒绝其他授权会话的 token，防止会话混淆
                if (string.IsNullOrEmpty(respNonce) || respNonce != flow.Nonce) continue;
                if (string.IsNullOrEmpty(token)) continue;

                string name = null;
                Dictionary<string, string> apiHeaders = BuildHeaders(token, flow.MachineId);
                string infoUid;
                FetchUserInfo(apiHeaders, out infoUid, out name);
                if (string.IsNullOrEmpty(uid)) uid = infoUid;
                if (string.IsNullOrEmpty(uid)) uid = Hash.Sha256Hex(token);

                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                SecretPayload secret = new SecretPayload();
                secret.Platform = PlatformIds.Qoder;
                secret.Token = token;
                secret.RefreshToken = refresh;
                secret.Uid = uid;
                secret.Name = name;
                secret.MachineId = flow.MachineId;
                secret.Kind = "device";
                secret.ExpiresAt = expiresIn > 0 ? now + NormalizeExpiresIn(expiresIn) : 0;
                secret.RefreshExpiresAt = refreshExpiresIn > 0 ? now + NormalizeExpiresIn(refreshExpiresIn) : 0;
                secret.UpdatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

                AccountDraft d = new AccountDraft();
                d.Platform = PlatformIds.Qoder;
                d.Uid = uid;
                d.Name = string.IsNullOrEmpty(name) ? "Qoder " + Hash.Mask(uid, 4, 4) : name;
                d.Source = "oauth";
                d.Secret = secret;
                if (progress != null) progress("授权成功", 60);
                return d;
            }
            throw new Exception("授权超时：未在 3 分钟内完成浏览器登录");
        }

        public CheckinOutcome Run(Account account, SecretPayload secret, Action<SecretPayload> persist, bool dryRun)
        {
            if (secret == null || string.IsNullOrEmpty(secret.Token))
                return CheckinOutcome.Make(OutcomeKind.AuthRequired, -1, "Qoder 未登录，请重新添加账号");

            SecretPayload current = secret;
            if (NeedsRefresh(current))
            {
                SecretPayload r = Refresh(current);
                if (r != null)
                {
                    current = r;
                    if (persist != null) persist(current);
                }
            }

            Dictionary<string, string> headers = BuildHeaders(current.Token, current.MachineId);
            ApiResponse list = HttpApi.Get(OpenApi + CampaignsPath, headers, 25);
            if (list.NetworkError)
                return CheckinOutcome.Make(OutcomeKind.Transient, -3, "网络异常：" + list.Error);
            if (list.Status == 401 || list.Status == 403)
            {
                SecretPayload r = Refresh(current);
                if (r != null)
                {
                    current = r;
                    if (persist != null) persist(current);
                    headers = BuildHeaders(current.Token, current.MachineId);
                    list = HttpApi.Get(OpenApi + CampaignsPath, headers, 25);
                }
            }
            if (list.NetworkError)
                return CheckinOutcome.Make(OutcomeKind.Transient, -3, "网络异常：" + list.Error);
            if (list.Status == 401 || list.Status == 403)
                return CheckinOutcome.Make(OutcomeKind.AuthRequired, -6, "Qoder 登录已失效，请重新登录");
            if (list.Status == 429)
                return CheckinOutcome.Make(OutcomeKind.Transient, 429, "请求过于频繁，稍后重试");
            if (!list.Ok)
                return CheckinOutcome.Make(OutcomeKind.Transient, list.Status, "Qoder 服务返回 HTTP " + list.Status);

            List<QoderCampaign> campaigns = ParseCampaigns(list.Body, DateTime.Now);
            QoderCampaign target = null;
            QoderCampaign claimed = null;
            for (int i = 0; i < campaigns.Count; i++)
            {
                QoderCampaign c = campaigns[i];
                if (c.ClaimStatus == "CLAIMABLE" && target == null) target = c;
                else if (c.ClaimStatus == "CLAIMED" && claimed == null) claimed = c;
            }
            if (target == null)
            {
                if (claimed != null)
                {
                    CheckinOutcome already = CheckinOutcome.Make(OutcomeKind.Already, 0, "今日已领取");
                    already.Credits = claimed.Amount;
                    already.Confirmed = true;
                    return already;
                }
                return CheckinOutcome.Make(OutcomeKind.Pending, 0, "今日暂无可领取活动（10:00 刷新）");
            }
            if (dryRun)
            {
                CheckinOutcome dry = CheckinOutcome.Make(OutcomeKind.Pending, 0, "发现可领取：+" + target.Amount + " Credits");
                dry.Credits = target.Amount;
                return dry;
            }

            string url = OpenApi + "/sash/api/v1/me/campaigns/" + Uri.EscapeDataString(target.CampaignId) + "/claim";
            Dictionary<string, string> claimHeaders = BuildHeaders(current.Token, current.MachineId);
            claimHeaders["Origin"] = WebBase;
            ApiResponse claim = HttpApi.Post(url, claimHeaders, "{}", 25);
            if (claim.NetworkError)
                return CheckinOutcome.Make(OutcomeKind.Transient, -3, "网络异常：" + claim.Error);
            if (claim.Status == 401 || claim.Status == 403)
                return CheckinOutcome.Make(OutcomeKind.AuthRequired, -6, "Qoder 登录已失效，请重新登录");
            if (claim.Status == 429)
                return CheckinOutcome.Make(OutcomeKind.Transient, 429, "请求过于频繁，稍后重试");

            Dictionary<string, object> claimBody = JsonUtil.ParseObject(claim.Body);
            string result = claimBody == null ? null : JsonUtil.GetStringAny(claimBody, "result", "status");
            bool replayed = claimBody != null && JsonUtil.GetBool(claimBody, "replayed");
            if (claim.Status == 409 || replayed ||
                (result != null && result.Equals("ALREADY_CLAIMED", StringComparison.OrdinalIgnoreCase)))
            {
                CheckinOutcome already = CheckinOutcome.Make(OutcomeKind.Already, 0, "今日已领取");
                already.Credits = target.Amount;
                already.Confirmed = true;
                return already;
            }
            if (!claim.Ok)
                return CheckinOutcome.Make(OutcomeKind.Transient, claim.Status, "领取失败（HTTP " + claim.Status + "）");

            int amount = target.Amount > 0 ? target.Amount : 100;
            ApiResponse confirm = HttpApi.Get(OpenApi + CampaignsPath, headers, 25);
            if (confirm.NetworkError)
            {
                CheckinOutcome unsure = CheckinOutcome.Make(OutcomeKind.SuccessUnconfirmed, 0, "已提交领取，结果待确认");
                unsure.Credits = amount;
                return unsure;
            }
            List<QoderCampaign> after = ParseCampaigns(confirm.Body, DateTime.Now);
            bool claimedNow = false;
            for (int i = 0; i < after.Count; i++)
            {
                if (after[i].CampaignId == target.CampaignId && after[i].ClaimStatus == "CLAIMED")
                {
                    claimedNow = true;
                    if (after[i].Amount > 0) amount = after[i].Amount;
                    break;
                }
            }
            if (claimedNow)
            {
                CheckinOutcome ok = CheckinOutcome.Make(OutcomeKind.Success, 0, "领取成功");
                ok.Credits = amount;
                ok.Confirmed = true;
                return ok;
            }
            CheckinOutcome unconfirmed = CheckinOutcome.Make(OutcomeKind.SuccessUnconfirmed, 0, "已提交领取，但未确认到已领状态");
            unconfirmed.Credits = amount;
            return unconfirmed;
        }

        public bool NeedsRefresh(SecretPayload secret)
        {
            if (secret == null) return false;
            if (string.IsNullOrEmpty(secret.RefreshToken) && string.IsNullOrEmpty(secret.Pat)) return false;
            if (secret.ExpiresAt <= 0) return false;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return now >= secret.ExpiresAt - 5 * 60 * 1000;
        }

        public SecretPayload Refresh(SecretPayload secret)
        {
            if (secret == null) return null;
            SecretPayload next = null;
            if (!string.IsNullOrEmpty(secret.RefreshToken))
                next = RefreshWithToken(secret.RefreshToken, secret.Uid, secret);

            // PAT 通道兜底：作业令牌 24h 过期且没有 refresh token 时，用原始 PAT 重新换发
            if (next == null && !string.IsNullOrEmpty(secret.Pat))
            {
                next = ExchangePat(secret.Pat);
                if (next != null)
                {
                    next.Uid = secret.Uid;
                    next.Name = secret.Name;
                    next.MachineId = secret.MachineId;
                }
            }
            if (next != null && string.IsNullOrEmpty(next.MachineId)) next.MachineId = secret.MachineId;
            return next;
        }

        private SecretPayload RefreshWithToken(string refreshToken, string uid, SecretPayload old)
        {
            try
            {
                bool job = refreshToken.StartsWith("jrt-", StringComparison.Ordinal);
                string url = OpenApi + (job ? JobRefreshPath : DeviceRefreshPath);
                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers["User-Agent"] = "Qoder";
                headers["Accept"] = "application/json";
                Dictionary<string, object> req = new Dictionary<string, object>();
                req["refresh_token"] = refreshToken;
                ApiResponse resp = HttpApi.Post(url, headers, JsonUtil.Serialize(req), 25);
                if (resp.NetworkError || !resp.Ok) return null;
                Dictionary<string, object> root = JsonUtil.ParseObject(resp.Body);
                if (root == null) return null;
                Dictionary<string, object> data = JsonUtil.GetDict(root, "data");
                if (data == null) data = root;

                string token = JsonUtil.GetStringAny(data, "token", "access_token", "accessToken", "device_token");
                if (string.IsNullOrEmpty(token)) token = JsonUtil.GetStringAny(root, "token", "access_token", "accessToken");
                if (string.IsNullOrEmpty(token)) return null;
                string rt = JsonUtil.GetStringAny(data, "refresh_token", "refreshToken");
                if (string.IsNullOrEmpty(rt)) rt = refreshToken;
                string newUid = JsonUtil.GetStringAny(data, "user_id", "userId", "uid");
                if (string.IsNullOrEmpty(newUid)) newUid = uid;
                string newName = JsonUtil.GetStringAny(data, "user_name", "username", "nickname");

                SecretPayload n = new SecretPayload();
                n.Platform = PlatformIds.Qoder;
                n.Token = token;
                n.RefreshToken = rt;
                n.Uid = string.IsNullOrEmpty(newUid) ? Hash.Sha256Hex(token) : newUid;
                n.Name = string.IsNullOrEmpty(newName) ? (old == null ? null : old.Name) : newName;
                n.MachineId = old == null || string.IsNullOrEmpty(old.MachineId) ? NewMachineId() : old.MachineId;
                n.Pat = old == null ? null : old.Pat;
                n.Kind = old == null ? "device" : old.Kind;
                n.ExpiresAt = ParseExpiry(data);
                n.RefreshExpiresAt = old == null ? 0 : old.RefreshExpiresAt;
                n.UpdatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
                return n;
            }
            catch (Exception ex)
            {
                Logger.Warn("qoder", "刷新 token 失败: " + ex.Message);
                return null;
            }
        }

        private static SecretPayload ExchangePat(string pat)
        {
            try
            {
                string[] paths = new string[] { JobTokenPath, "/api/v1/jobToken/exchange", "/api/v1/jobToken/exchange" };
                for (int i = 0; i < paths.Length; i++)
                {
                    Dictionary<string, string> headers = new Dictionary<string, string>();
                    headers["Authorization"] = "Bearer " + pat;
                    headers["Cosy-ClientType"] = "10";
                    headers["User-Agent"] = "Qoder";
                    headers["Accept"] = "application/json";

                    object body;
                    if (i == 0)
                    {
                        Dictionary<string, object> b = new Dictionary<string, object>();
                        b["clientId"] = StableUuid(pat);
                        body = b;
                    }
                    else if (i == 1)
                    {
                        Dictionary<string, object> b2 = new Dictionary<string, object>();
                        b2["pat"] = pat;
                        body = b2;
                    }
                    else body = new Dictionary<string, object>();

                    ApiResponse resp = HttpApi.Post(OpenApi + paths[i], headers, JsonUtil.Serialize(body), 25);
                    if (resp.NetworkError || resp.Status != 200) continue;
                    Dictionary<string, object> root = JsonUtil.ParseObject(resp.Body);
                    if (root == null) continue;
                    Dictionary<string, object> data = JsonUtil.GetDict(root, "data");
                    if (data == null) data = root;
                    string token = JsonUtil.GetStringAny(data, "token", "access_token", "accessToken", "job_token");
                    if (string.IsNullOrEmpty(token)) continue;

                    SecretPayload p = new SecretPayload();
                    p.Platform = PlatformIds.Qoder;
                    p.Token = token;
                    p.RefreshToken = JsonUtil.GetStringAny(data, "refresh_token", "refreshToken");
                    p.Pat = pat;
                    p.Kind = "pat";
                    p.ExpiresAt = ParseExpiry(data);
                    p.MachineId = NewMachineId();
                    return p;
                }
                return null;
            }
            catch { return null; }
        }

        private static void FetchUserInfo(Dictionary<string, string> headers, out string uid, out string name)
        {
            uid = null;
            name = null;
            try
            {
                ApiResponse resp = HttpApi.Post(OpenApi + UserInfoPath, headers, "{}", 20);
                if (resp.NetworkError || !resp.Ok) return;
                Dictionary<string, object> root = JsonUtil.ParseObject(resp.Body);
                if (root == null) return;
                Dictionary<string, object> data = JsonUtil.GetDict(root, "data");
                if (data == null) data = root;
                uid = JsonUtil.GetStringAny(data, "uid", "id", "user_id", "userId");
                name = JsonUtil.GetStringAny(data, "nickname", "name", "username", "user_name");
            }
            catch { }
        }

        private static List<QoderCampaign> ParseCampaigns(string json, DateTime today)
        {
            List<QoderCampaign> list = new List<QoderCampaign>();
            try
            {
                Dictionary<string, object> root = JsonUtil.ParseObject(json);
                if (root == null) return list;
                object arrObj = null;
                root.TryGetValue("campaigns", out arrObj);
                if (!(arrObj is ArrayList))
                {
                    Dictionary<string, object> data = JsonUtil.GetDict(root, "data");
                    if (data != null) data.TryGetValue("campaigns", out arrObj);
                }
                ArrayList arr = arrObj as ArrayList;
                if (arr == null) return list;
                long nowSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                DateTime todayDate = today.Date;
                for (int i = 0; i < arr.Count; i++)
                {
                    Dictionary<string, object> d = arr[i] as Dictionary<string, object>;
                    if (d == null) continue;
                    QoderCampaign c = new QoderCampaign();
                    c.CampaignId = JsonUtil.GetString(d, "campaignId");
                    c.ActionType = JsonUtil.GetString(d, "actionType");
                    c.ClaimStatus = JsonUtil.GetString(d, "claimStatus");
                    Dictionary<string, object> benefit = JsonUtil.GetDict(d, "benefit");
                    int amount = benefit == null ? 0 : JsonUtil.GetInt(benefit, "amount");
                    c.Amount = amount < 0 ? 0 : amount;
                    c.StartAt = JsonUtil.GetLong(d, "startAt");
                    c.EndAt = JsonUtil.GetLong(d, "endAt");
                    if (string.IsNullOrEmpty(c.CampaignId)) continue;
                    if (c.ActionType != "CLAIM_BENEFIT") continue;
                    if (c.StartAt > 0)
                    {
                        DateTime campDate = DateTimeOffset.FromUnixTimeSeconds(c.StartAt).LocalDateTime.Date;
                        if (campDate < todayDate) continue;
                    }
                    if (c.EndAt > 0 && c.EndAt <= nowSec) continue;
                    list.Add(c);
                }
            }
            catch { }
            return list;
        }

        private static Dictionary<string, string> BuildHeaders(string token, string machineId)
        {
            Dictionary<string, string> h = new Dictionary<string, string>();
            h["Authorization"] = "Bearer " + token;
            h["User-Agent"] = "Qoder";
            h["Accept"] = "application/json";
            h["cosy-clienttype"] = "10";
            h["Cosy-Version"] = CosyVersion;
            h["Cosy-MachineOS"] = "x86_64_win32";
            h["Cosy-MachineId"] = string.IsNullOrEmpty(machineId) ? NewMachineId() : machineId;
            try { h["Cosy-MachineHostname"] = Environment.MachineName; } catch { }
            return h;
        }

        private static long ParseExpiry(Dictionary<string, object> data)
        {
            long expiresAt = JsonUtil.GetLong(data, "expiresAt");
            if (expiresAt > 0) return expiresAt < 100000000000L ? expiresAt * 1000 : expiresAt;
            long expiresIn = JsonUtil.GetLong(data, "expiresIn");
            if (expiresIn <= 0) expiresIn = JsonUtil.GetLong(data, "expires_in");
            if (expiresIn <= 0) return 0;
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + NormalizeExpiresIn(expiresIn);
        }

        internal static long NormalizeExpiresIn(long e)
        {
            // Qoder 抓包：86400000 已是毫秒；阈值 2592000（30 天秒数）用于区分秒/毫秒
            return e > 2592000L ? e : e * 1000;
        }

        private static string NewMachineId()
        {
            return Hash.RandomHex(16);
        }

        private static string StableUuid(string seed)
        {
            byte[] hash;
            using (SHA256 sha = SHA256.Create())
                hash = sha.ComputeHash(Encoding.UTF8.GetBytes(seed == null ? "qoder" : seed));
            byte[] bytes = new byte[16];
            Buffer.BlockCopy(hash, 0, bytes, 0, 16);
            bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
            return new Guid(bytes).ToString();
        }
    }
}
