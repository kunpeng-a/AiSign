using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using AiSign.Core;
using AiSign.Net;

namespace AiSign.Platforms
{
    internal sealed class WorkBuddyPlatform : ICheckinPlatform
    {
        private const string DefaultDomain = "https://www.workbuddy.cn";
        private const string BackupDomain = "https://www.codebuddy.cn";
        private const string StatePath = "/v2/plugin/auth/state?platform=workbuddy";
        private const string TokenPath = "/v2/plugin/auth/token?state=";
        private const string RefreshUrl = "https://copilot.tencent.com/v2/plugin/auth/token/refresh";
        private const string StatusPath = "/v2/billing/meter/checkin-activity-status";
        private const string ClaimPath = "/v2/billing/meter/daily-checkin";

        private static readonly string[] AuthRoots = new string[] { "CodeBuddyExtension", "CodeBuddy", "WorkBuddy", "CodeBuddyCN" };
        private static readonly string[] AuthFiles = new string[] { "workbuddy-desktop.info", "workbuddy-desktop-ai.info", "workbuddy-desktop-dev.info" };

        public string Id { get { return PlatformIds.WorkBuddy; } }
        public string DisplayName { get { return "WorkBuddy"; } }
        public bool SupportsMultiAccount { get { return true; } }
        public bool SupportsOAuth { get { return true; } }

        public List<AccountDraft> ScanLocal()
        {
            List<AccountDraft> result = new List<AccountDraft>();
            List<string> dirs = new List<string>();
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            for (int i = 0; i < AuthRoots.Length; i++)
            {
                string dir = Path.Combine(local, AuthRoots[i], "Data", "Public", "auth");
                if (HasAuthFile(dir) && !dirs.Contains(dir)) dirs.Add(dir);
            }

            List<string> found = ScanUtil.FindAuthDirs(local, AuthRoots, 4);
            for (int i = 0; i < found.Count; i++)
                if (!dirs.Contains(found[i])) dirs.Add(found[i]);

            List<string> seenUid = new List<string>();
            for (int i = 0; i < dirs.Count; i++)
            {
                AccountDraft d = ReadAuthDir(dirs[i]);
                if (d == null) continue;
                string key = string.IsNullOrEmpty(d.Uid) ? d.Name : d.Uid;
                if (!string.IsNullOrEmpty(key) && seenUid.Contains(key)) continue;
                if (!string.IsNullOrEmpty(key)) seenUid.Add(key);
                result.Add(d);
            }
            return result;
        }

        private static bool HasAuthFile(string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
                for (int i = 0; i < AuthFiles.Length; i++)
                    if (File.Exists(Path.Combine(dir, AuthFiles[i]))) return true;
                return Directory.GetFiles(dir, "*.info").Length > 0;
            }
            catch { return false; }
        }

        private static AccountDraft ReadAuthDir(string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;
                for (int i = 0; i < AuthFiles.Length; i++)
                {
                    AccountDraft d = ReadAuthFile(Path.Combine(dir, AuthFiles[i]));
                    if (d != null) return d;
                }
                foreach (string file in Directory.GetFiles(dir, "*.info"))
                {
                    AccountDraft d = ReadAuthFile(file);
                    if (d != null) return d;
                }
            }
            catch { }
            return null;
        }

        private static AccountDraft ReadAuthFile(string file)
        {
            try
            {
                if (!File.Exists(file)) return null;
                Dictionary<string, object> root = ScanUtil.ReadJson(file);
                if (root == null) return null;

                string uid = null;
                Dictionary<string, object> account = JsonUtil.GetDict(root, "account");
                if (account != null) uid = JsonUtil.GetString(account, "uid");

                Dictionary<string, object> auth = JsonUtil.GetDict(root, "auth");
                if (auth == null) return null;

                object atObj;
                string accessToken = null;
                bool encrypted = false;
                if (auth.TryGetValue("accessToken", out atObj))
                {
                    accessToken = atObj as string;
                    if (accessToken == null && atObj is Dictionary<string, object>)
                    {
                        Dictionary<string, object> envelope = (Dictionary<string, object>)atObj;
                        object flag;
                        if (envelope.TryGetValue("$wbEncrypted", out flag))
                        {
                            try { encrypted = Convert.ToInt32(flag) == 1; } catch { }
                        }
                    }
                }

                string domain = JsonUtil.GetString(auth, "domain");
                long expiresAt = JsonUtil.GetLong(auth, "expiresAt");
                long refreshExpiresAt = JsonUtil.GetLong(auth, "refreshExpiresAt");

                if (encrypted || string.IsNullOrEmpty(accessToken))
                {
                    AccountDraft warning = new AccountDraft();
                    warning.Platform = PlatformIds.WorkBuddy;
                    warning.Uid = uid;
                    warning.Name = string.IsNullOrEmpty(uid) ? "WorkBuddy 本机账号" : "WorkBuddy " + Hash.Mask(uid, 4, 4);
                    warning.Source = "scan";
                    warning.Warning = "本机凭证已加密（WorkBuddy 5.6+），无法离线读取，请改用浏览器授权";
                    warning.Secret = null;
                    return warning;
                }

                string identity = uid;
                if (string.IsNullOrEmpty(identity)) identity = JwtUtil.Sub(accessToken);
                if (string.IsNullOrEmpty(identity)) identity = Hash.Sha256Hex(accessToken);

                SecretPayload secret = new SecretPayload();
                secret.Platform = PlatformIds.WorkBuddy;
                secret.Token = accessToken;
                secret.Uid = identity;
                secret.Domain = string.IsNullOrEmpty(domain) ? DefaultDomain : NormalizeDomain(domain);
                secret.ExpiresAt = expiresAt;
                secret.RefreshExpiresAt = refreshExpiresAt;
                secret.UpdatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

                AccountDraft d = new AccountDraft();
                d.Platform = PlatformIds.WorkBuddy;
                d.Uid = identity;
                d.Name = string.IsNullOrEmpty(uid) ? "WorkBuddy 账号" : "WorkBuddy " + Hash.Mask(uid, 4, 4);
                d.Source = "scan";
                d.Secret = secret;
                return d;
            }
            catch { return null; }
        }

        public AccountDraft ValidatePastedToken(string raw)
        {
            if (string.IsNullOrEmpty(raw)) throw new Exception("请输入 WorkBuddy Token");
            string text = raw.Trim();
            string token = text;
            string refresh = null;
            string uid = null;
            string domain = DefaultDomain;
            long expiresAt = 0;
            long refreshExpiresAt = 0;

            if (text.StartsWith("{"))
            {
                Dictionary<string, object> root = JsonUtil.ParseObject(text);
                if (root == null) throw new Exception("JSON 无法解析");
                Dictionary<string, object> account = JsonUtil.GetDict(root, "account");
                if (account != null) uid = JsonUtil.GetString(account, "uid");
                Dictionary<string, object> auth = JsonUtil.GetDict(root, "auth");
                if (auth != null)
                {
                    token = JsonUtil.GetString(auth, "accessToken");
                    refresh = JsonUtil.GetString(auth, "refreshToken");
                    string dom = JsonUtil.GetString(auth, "domain");
                    if (!string.IsNullOrEmpty(dom)) domain = NormalizeDomain(dom);
                    expiresAt = JsonUtil.GetLong(auth, "expiresAt");
                    refreshExpiresAt = JsonUtil.GetLong(auth, "refreshExpiresAt");
                }
                if (string.IsNullOrEmpty(token)) throw new Exception("JSON 中未找到 auth.accessToken");
            }

            if (string.IsNullOrEmpty(token)) throw new Exception("Token 为空");
            bool looksJwt = token.IndexOf('.') > 0;
            if (!looksJwt)
            {
                SecretPayload refreshed = RefreshWithToken(token, uid, null);
                if (refreshed == null) throw new Exception("Refresh Token 换取 Access Token 失败");
                token = refreshed.Token;
                refresh = refreshed.RefreshToken;
                uid = string.IsNullOrEmpty(uid) ? refreshed.Uid : uid;
                expiresAt = refreshed.ExpiresAt;
                refreshExpiresAt = refreshed.RefreshExpiresAt;
            }

            Dictionary<string, string> headers = BuildHeaders(token, uid);
            ApiResponse status = PostAny(StatusPath, headers, "{}", 25, domain);
            if (status == null || status.NetworkError) throw new Exception("网络异常：" + (status == null ? "无响应" : status.Error));
            if (status.Status == 401) throw new Exception("Token 已失效（HTTP 401）");
            Dictionary<string, object> body = JsonUtil.ParseObject(status.Body);
            if (body == null) throw new Exception("接口返回无法解析（HTTP " + status.Status + "）");
            int code = JsonUtil.GetInt(body, "code");
            if (code != 0 && code != 10001)
            {
                string msg = JsonUtil.GetStringAny(body, "msg", "message");
                throw new Exception("Token 验证失败(" + code + ")：" + (msg == null ? "未知错误" : msg));
            }

            if (string.IsNullOrEmpty(uid)) uid = JwtUtil.Sub(token);
            if (string.IsNullOrEmpty(uid)) uid = Hash.Sha256Hex(token);

            SecretPayload secret = new SecretPayload();
            secret.Platform = PlatformIds.WorkBuddy;
            secret.Token = token;
            secret.RefreshToken = refresh;
            secret.Uid = uid;
            secret.Domain = domain;
            secret.ExpiresAt = expiresAt;
            secret.RefreshExpiresAt = refreshExpiresAt;
            secret.UpdatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

            AccountDraft d = new AccountDraft();
            d.Platform = PlatformIds.WorkBuddy;
            d.Uid = uid;
            d.Name = "WorkBuddy " + Hash.Mask(uid, 4, 4);
            d.Source = "token";
            d.Secret = secret;
            return d;
        }

        public AccountDraft RunOAuth(Action<string, int> progress, Func<bool> cancelled)
        {
            if (progress != null) progress("正在申请授权…", 0);
            string domain = null;
            string state = null;
            string authUrl = null;
            string[] domains = new string[] { DefaultDomain, BackupDomain };
            for (int i = 0; i < domains.Length; i++)
            {
                ApiResponse stateResp = HttpApi.Post(domains[i] + StatePath, null, "{}", 25);
                if (stateResp.NetworkError) continue;
                Dictionary<string, object> stateBody = JsonUtil.ParseObject(stateResp.Body);
                Dictionary<string, object> data = JsonUtil.GetDict(stateBody, "data");
                string s = data == null ? null : JsonUtil.GetString(data, "state");
                string u = data == null ? null : JsonUtil.GetString(data, "authUrl");
                if (!string.IsNullOrEmpty(s) && !string.IsNullOrEmpty(u))
                {
                    domain = domains[i];
                    state = s;
                    authUrl = u;
                    break;
                }
            }
            if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(authUrl))
                throw new Exception("授权服务响应异常（主备域名均未返回 state/authUrl）");

            BrowserLauncher.OpenInPrivate(authUrl);
            if (progress != null) progress("请在浏览器中完成登录确认（3 分钟内有效）", 0);

            for (int i = 0; i < 60; i++)
            {
                if (cancelled != null && cancelled()) throw new Exception("已取消授权");
                Thread.Sleep(3000);
                if (cancelled != null && cancelled()) throw new Exception("已取消授权");
                if (progress != null) progress("等待浏览器确认… 剩余 " + (180 - i * 3) + " 秒", i);
                ApiResponse tr = HttpApi.Get(domain + TokenPath + Uri.EscapeDataString(state), null, 25);
                if (tr.NetworkError) continue;
                Dictionary<string, object> tb = JsonUtil.ParseObject(tr.Body);
                if (tb == null) continue;
                int code = JsonUtil.GetInt(tb, "code");
                if (code == 11217) continue;
                if (code != 0)
                    throw new Exception("授权失败(" + code + ")：" + (JsonUtil.GetStringAny(tb, "msg", "message") ?? ""));
                Dictionary<string, object> tdata = JsonUtil.GetDict(tb, "data");
                if (tdata == null) continue;
                string at = JsonUtil.GetStringAny(tdata, "accessToken", "access_token");
                if (string.IsNullOrEmpty(at))
                {
                    Dictionary<string, object> nested = JsonUtil.GetDict(tdata, "auth");
                    if (nested == null) nested = JsonUtil.GetDict(tdata, "token");
                    if (nested != null) at = JsonUtil.GetStringAny(nested, "accessToken", "access_token");
                }
                if (string.IsNullOrEmpty(at)) continue;
                string rt = JsonUtil.GetStringAny(tdata, "refreshToken", "refresh_token");
                if (string.IsNullOrEmpty(rt))
                {
                    Dictionary<string, object> nested2 = JsonUtil.GetDict(tdata, "auth");
                    if (nested2 != null) rt = JsonUtil.GetStringAny(nested2, "refreshToken", "refresh_token");
                }
                string uid = JsonUtil.GetStringAny(tdata, "uid", "userId");
                if (string.IsNullOrEmpty(uid)) uid = JwtUtil.Sub(at);
                if (string.IsNullOrEmpty(uid)) uid = Hash.Sha256Hex(at);
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                long expiresIn = JsonUtil.GetLong(tdata, "expiresIn");
                long refreshExpiresIn = JsonUtil.GetLong(tdata, "refreshExpiresIn");

                SecretPayload secret = new SecretPayload();
                secret.Platform = PlatformIds.WorkBuddy;
                secret.Token = at;
                secret.RefreshToken = rt;
                secret.Uid = uid;
                secret.Domain = domain;
                secret.ExpiresAt = expiresIn > 0 ? now + expiresIn * 1000 : 0;
                secret.RefreshExpiresAt = refreshExpiresIn > 0 ? now + refreshExpiresIn * 1000 : 0;
                secret.UpdatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

                AccountDraft d = new AccountDraft();
                d.Platform = PlatformIds.WorkBuddy;
                d.Uid = uid;
                d.Name = "WorkBuddy " + Hash.Mask(uid, 4, 4);
                d.Source = "oauth";
                d.Secret = secret;
                if (progress != null) progress("授权成功", 60);
                return d;
            }
            throw new Exception("授权超时：未在 3 分钟内完成浏览器确认");
        }

        public CheckinOutcome Run(Account account, SecretPayload secret, Action<SecretPayload> persist, bool dryRun)
        {
            if (secret == null || string.IsNullOrEmpty(secret.Token))
                return CheckinOutcome.Make(OutcomeKind.AuthRequired, -1, "WorkBuddy 未授权，请重新登录授权");

            SecretPayload current = secret;
            if (NeedsRefresh(current))
            {
                SecretPayload refreshed = Refresh(current);
                if (refreshed != null)
                {
                    current = refreshed;
                    if (persist != null) persist(current);
                }
            }

            Dictionary<string, string> headers = BuildHeaders(current.Token, current.Uid);
            ApiResponse status = PostAny(StatusPath, headers, "{}", 25, current.Domain);
            if (status != null && IsAuthFailure(status))
            {
                SecretPayload refreshed = Refresh(current);
                if (refreshed != null)
                {
                    current = refreshed;
                    if (persist != null) persist(current);
                    headers = BuildHeaders(current.Token, current.Uid);
                    status = PostAny(StatusPath, headers, "{}", 25, current.Domain);
                }
            }
            if (status == null || status.NetworkError)
                return CheckinOutcome.Make(OutcomeKind.Transient, -3, "网络异常：" + (status == null ? "无响应" : status.Error));
            if (IsAuthFailure(status))
                return CheckinOutcome.Make(OutcomeKind.AuthRequired, -6, "WorkBuddy 登录已失效，请重新授权");

            Dictionary<string, object> body = JsonUtil.ParseObject(status.Body);
            int code = body == null ? status.Status : JsonUtil.GetInt(body, "code");
            if (code != 0)
            {
                string msg = body == null ? null : JsonUtil.GetStringAny(body, "msg", "message");
                return CheckinOutcome.Make(OutcomeKind.Transient, code, msg == null ? "状态查询失败" : msg);
            }
            Dictionary<string, object> data = JsonUtil.GetDict(body, "data");
            if (data == null)
                return CheckinOutcome.Make(OutcomeKind.Transient, status.Status, "状态响应异常");

            bool checkedIn = JsonUtil.GetBoolAny(data, "todayCheckedIn", "today_checked_in");
            int todayCredit = JsonUtil.GetIntAny(data, "todayCredit", "today_credit", "dailyCredit", "daily_credit");
            if (checkedIn)
            {
                CheckinOutcome already = CheckinOutcome.Make(OutcomeKind.Already, 10001, "今日已签到");
                already.Credits = todayCredit;
                already.Confirmed = true;
                return already;
            }
            if (dryRun)
            {
                CheckinOutcome dry = CheckinOutcome.Make(OutcomeKind.Pending, 0, "预检通过，今日可签到");
                dry.Credits = todayCredit;
                return dry;
            }

            ApiResponse claim = PostAny(ClaimPath, headers, "{}", 25, current.Domain);
            if (claim == null || claim.NetworkError)
                return CheckinOutcome.Make(OutcomeKind.Transient, -3, "网络异常：" + (claim == null ? "无响应" : claim.Error));
            if (IsAuthFailure(claim))
                return CheckinOutcome.Make(OutcomeKind.AuthRequired, -6, "WorkBuddy 登录已失效，请重新授权");

            Dictionary<string, object> claimBody = JsonUtil.ParseObject(claim.Body);
            int ccode = claimBody == null ? claim.Status : JsonUtil.GetInt(claimBody, "code");
            string cmsg = claimBody == null ? null : JsonUtil.GetStringAny(claimBody, "msg", "message");
            if (ccode == 41000)
                return CheckinOutcome.Make(OutcomeKind.Pending, 41000, "签到活动未开始或已结束");
            bool claimOk = ccode == 0 || ccode == 10001 || (cmsg != null && cmsg.IndexOf("已签到", StringComparison.Ordinal) >= 0);
            if (!claimOk)
                return CheckinOutcome.Make(OutcomeKind.Transient, ccode, cmsg == null ? "签到失败" : cmsg);

            ApiResponse confirm = PostAny(StatusPath, headers, "{}", 25, current.Domain);
            Dictionary<string, object> cb = confirm == null || confirm.NetworkError ? null : JsonUtil.ParseObject(confirm.Body);
            Dictionary<string, object> cdata = cb == null ? null : JsonUtil.GetDict(cb, "data");
            bool confirmed = cdata != null && JsonUtil.GetBoolAny(cdata, "todayCheckedIn", "today_checked_in");
            int finalCredit = todayCredit;
            if (cdata != null)
            {
                int fc = JsonUtil.GetIntAny(cdata, "todayCredit", "today_credit", "dailyCredit", "daily_credit");
                if (fc > 0) finalCredit = fc;
            }
            if (!confirmed && ccode != 10001)
            {
                CheckinOutcome unsure = CheckinOutcome.Make(OutcomeKind.SuccessUnconfirmed, ccode, "已提交签到，但未确认到已签状态");
                unsure.Credits = finalCredit;
                return unsure;
            }
            CheckinOutcome ok = CheckinOutcome.Make(ccode == 10001 ? OutcomeKind.Already : OutcomeKind.Success, 0,
                ccode == 10001 ? "今日已签到" : "签到成功");
            ok.Credits = finalCredit;
            ok.Confirmed = true;
            return ok;
        }

        public bool NeedsRefresh(SecretPayload secret)
        {
            if (secret == null) return false;
            if (string.IsNullOrEmpty(secret.RefreshToken)) return false;
            if (secret.ExpiresAt <= 0) return false;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return now >= secret.ExpiresAt - 5 * 60 * 1000;
        }

        public SecretPayload Refresh(SecretPayload secret)
        {
            if (secret == null || string.IsNullOrEmpty(secret.RefreshToken)) return null;
            return RefreshWithToken(secret.RefreshToken, secret.Uid, secret);
        }

        private SecretPayload RefreshWithToken(string refreshToken, string uid, SecretPayload old)
        {
            if (string.IsNullOrEmpty(refreshToken)) return null;
            try
            {
                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers["X-Refresh-Token"] = refreshToken;
                headers["X-Auth-Refresh-Source"] = "plugin";
                if (old != null && !string.IsNullOrEmpty(old.Token)) headers["Authorization"] = "Bearer " + old.Token;
                if (!string.IsNullOrEmpty(uid)) headers["X-User-Id"] = uid;

                ApiResponse resp = HttpApi.Post(RefreshUrl, headers, "{}", 25);
                if (resp.NetworkError) return null;
                Dictionary<string, object> body = JsonUtil.ParseObject(resp.Body);
                if (body == null || JsonUtil.GetInt(body, "code") != 0) return null;
                Dictionary<string, object> data = JsonUtil.GetDict(body, "data");
                if (data == null) return null;

                string at = JsonUtil.GetStringAny(data, "accessToken", "access_token");
                if (string.IsNullOrEmpty(at)) return null;
                string rt = JsonUtil.GetStringAny(data, "refreshToken", "refresh_token");
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                long expiresIn = JsonUtil.GetLong(data, "expiresIn");
                long refreshExpiresIn = JsonUtil.GetLong(data, "refreshExpiresIn");

                SecretPayload n = new SecretPayload();
                n.Platform = PlatformIds.WorkBuddy;
                n.Token = at;
                n.RefreshToken = string.IsNullOrEmpty(rt) ? refreshToken : rt;
                n.Uid = old != null && !string.IsNullOrEmpty(old.Uid) ? old.Uid : uid;
                if (string.IsNullOrEmpty(n.Uid)) n.Uid = JwtUtil.Sub(at);
                n.Domain = old != null && !string.IsNullOrEmpty(old.Domain) ? old.Domain : DefaultDomain;
                n.ExpiresAt = expiresIn > 0 ? now + expiresIn * 1000 : (old == null ? 0 : old.ExpiresAt);
                n.RefreshExpiresAt = refreshExpiresIn > 0 ? now + refreshExpiresIn * 1000 : (old == null ? 0 : old.RefreshExpiresAt);
                n.UpdatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
                return n;
            }
            catch (Exception ex)
            {
                Logger.Warn("wb", "刷新 token 失败: " + ex.Message);
                return null;
            }
        }

        private static Dictionary<string, string> BuildHeaders(string token, string uid)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["Authorization"] = "Bearer " + token;
            if (!string.IsNullOrEmpty(uid)) headers["X-User-Id"] = uid;
            return headers;
        }

        private static ApiResponse PostAny(string path, Dictionary<string, string> headers, string body, int timeout, string preferredDomain)
        {
            List<string> domains = new List<string>();
            if (!string.IsNullOrEmpty(preferredDomain)) domains.Add(NormalizeDomain(preferredDomain));
            if (!domains.Contains(DefaultDomain)) domains.Add(DefaultDomain);
            if (!domains.Contains(BackupDomain)) domains.Add(BackupDomain);

            ApiResponse last = null;
            for (int i = 0; i < domains.Count; i++)
            {
                Dictionary<string, string> h = new Dictionary<string, string>(headers);
                h["X-Domain"] = domains[i].Replace("https://", string.Empty);
                ApiResponse r = HttpApi.Post(domains[i] + path, h, body, timeout);
                if (!r.NetworkError) return r;
                last = r;
            }
            return last;
        }

        private static bool IsAuthFailure(ApiResponse resp)
        {
            if (resp == null) return false;
            if (resp.Status == 401 || resp.Status == 403) return true;
            Dictionary<string, object> body = JsonUtil.ParseObject(resp.Body);
            if (body == null) return false;
            int code = JsonUtil.GetInt(body, "code");
            return code == 401 || code == 40100 || code == 12153;
        }

        private static string NormalizeDomain(string domain)
        {
            if (string.IsNullOrEmpty(domain)) return DefaultDomain;
            if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return domain;
            return "https://" + domain;
        }

    }
}
