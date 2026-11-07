using System;
using System.Collections.Generic;

#pragma warning disable 0649

namespace AiSign.Core
{
    internal static class PlatformIds
    {
        public const string Trae = "trae";
        public const string WorkBuddy = "wb";
        public const string Qoder = "qoder";

        public static string DisplayName(string platform)
        {
            if (platform == Trae) return "Trae CN";
            if (platform == WorkBuddy) return "WorkBuddy";
            if (platform == Qoder) return "Qoder CN";
            return platform;
        }

        public static string[] All()
        {
            return new string[] { Trae, WorkBuddy, Qoder };
        }
    }

    internal static class AccountState
    {
        public const string Ok = "ok";
        public const string Expiring = "expiring";
        public const string NeedsReauth = "needs_reauth";
        public const string Locked = "locked";
    }

    internal static class CheckinStatus
    {
        public const string Success = "success";
        public const string Already = "already";
        public const string Pending = "pending";
        public const string Failed = "failed";
        public const string Auth = "auth";
        public const string Occupied = "occupied";
        public const string Skipped = "skipped";
        public const string GivenUp = "given_up";
    }

    internal sealed class Account
    {
        public string Id;
        public string Platform;
        public string Uid;
        public string Name;
        public string Remark;
        public string Source;
        public string AuthState;
        public bool Enabled;
        public string AddedAt;
        public string VerifiedAt;
        public string CredentialUpdatedAt;
        public int Sort;
    }

    internal sealed class AccountsFile
    {
        public int V = 1;
        public List<Account> Accounts = new List<Account>();
    }

    internal sealed class SecretPayload
    {
        public string Platform;
        public string Token;
        public string RefreshToken;
        public string Uid;
        public string Name;
        public string Domain;
        public string DeviceId;
        public string Region;
        public string MachineId;
        public string Pat;
        public string Kind;
        public long ExpiresAt;
        public long RefreshExpiresAt;
        public string UpdatedAt;
    }

    internal sealed class SecretItem
    {
        public string UpdatedAt;
        public string Blob;
    }

    internal sealed class SecretsFile
    {
        public int V = 1;
        public string Alg = "dpapi-cu";
        public string Entropy = "aisign-v1";
        public Dictionary<string, SecretItem> Items = new Dictionary<string, SecretItem>();
    }

    internal sealed class PlatformSetting
    {
        public bool Enabled = true;
        public string Target = "00:05";
        public int JitterSec = 120;
    }

    internal sealed class RetrySetting
    {
        public int[] Minutes = new int[] { 5, 15, 30, 60, 120 };
        public int GiveUpHour = 23;
    }

    internal sealed class NotifySetting
    {
        public string Mode = "failures";
        public bool Balloon = true;
    }

    internal sealed class NetworkSetting
    {
        public bool UseSystemProxy = true;
        public int TimeoutSec = 25;
    }

    internal sealed class WindowSetting
    {
        public double W = 1080;
        public double H = 720;
        public string LastPage = "accounts";
    }

    internal sealed class AdvancedSetting
    {
        public string LogLevel = "info";
        public int KeepResultDays = 180;
        public bool QoderUmidEnabled = true;
    }

    internal sealed class Settings
    {
        public int V = 1;
        public string Theme = "dark";
        public bool LaunchAtLogin = false;
        public bool StartMinimized = true;
        public bool CloseToTray = true;
        public int StartupDelaySec = 20;
        public bool AutoCheckin = true;
        public int MaxConcurrency = 2;
        public int AccountGapMs = 1500;
        public Dictionary<string, PlatformSetting> Platforms = new Dictionary<string, PlatformSetting>();
        public RetrySetting Retry = new RetrySetting();
        public NotifySetting Notify = new NotifySetting();
        public NetworkSetting Network = new NetworkSetting();
        public WindowSetting Window = new WindowSetting();
        public AdvancedSetting Advanced = new AdvancedSetting();

        public static Settings CreateDefault()
        {
            Settings s = new Settings();
            s.Platforms[PlatformIds.Trae] = new PlatformSetting();
            s.Platforms[PlatformIds.Trae].Target = "00:05";
            s.Platforms[PlatformIds.Trae].JitterSec = 120;
            s.Platforms[PlatformIds.WorkBuddy] = new PlatformSetting();
            s.Platforms[PlatformIds.WorkBuddy].Target = "00:05";
            s.Platforms[PlatformIds.WorkBuddy].JitterSec = 120;
            s.Platforms[PlatformIds.Qoder] = new PlatformSetting();
            s.Platforms[PlatformIds.Qoder].Target = "10:05";
            s.Platforms[PlatformIds.Qoder].JitterSec = 90;
            return s;
        }

        public PlatformSetting Platform(string platform)
        {
            PlatformSetting p;
            if (Platforms != null && Platforms.TryGetValue(platform, out p) && p != null) return p;
            p = new PlatformSetting();
            if (platform == PlatformIds.Qoder) { p.Target = "10:05"; p.JitterSec = 90; }
            return p;
        }
    }

    internal sealed class CheckinRecord
    {
        public string Platform;
        public string AccountId;
        public string Name;
        public string Status;
        public int Code;
        public string Message;
        public int Credits;
        public int Attempts;
        public bool Confirmed;
        public string StartedAt;
        public string FinishedAt;
    }

    internal sealed class ResultsFile
    {
        public int V = 1;
        public Dictionary<string, Dictionary<string, CheckinRecord>> Days =
            new Dictionary<string, Dictionary<string, CheckinRecord>>();
    }

    internal sealed class AccountRunState
    {
        public string Status;
        public int Attempts;
        public string NextRetryAt;
        public int LastCode;
        public string LastMessage;
        public string UpdatedAt;
    }

    internal sealed class SchedulerStateFile
    {
        public int V = 1;
        public string Day;
        public Dictionary<string, AccountRunState> Accounts = new Dictionary<string, AccountRunState>();
        public string LastTickAt;
        public string PausedUntil;
    }

    internal enum OutcomeKind
    {
        Success,
        SuccessUnconfirmed,
        Already,
        Pending,
        Transient,
        AuthRequired,
        Occupied,
        Permanent
    }

    internal sealed class CheckinOutcome
    {
        public OutcomeKind Kind;
        public int Code;
        public string Message;
        public int Credits;
        public bool Confirmed;

        public static CheckinOutcome Make(OutcomeKind kind, int code, string message)
        {
            CheckinOutcome o = new CheckinOutcome();
            o.Kind = kind;
            o.Code = code;
            o.Message = message;
            return o;
        }
    }

    internal sealed class AccountDraft
    {
        public string Platform;
        public string Uid;
        public string Name;
        public string Source;
        public SecretPayload Secret;
        public string Warning;
        public bool IdStable = true;
    }
}

#pragma warning restore 0649
