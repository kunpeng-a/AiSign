using System;
using System.Globalization;
using AiSign.Core;

namespace AiSign.Services
{
    internal static class SchedulerPolicy
    {
        public static string DayKey(DateTime now)
        {
            return now.ToString("yyyy-MM-dd");
        }

        public static int JitterSec(string accountId, string day, int maxSec)
        {
            if (maxSec <= 0) return 0;
            string hex = Hash.Sha256Hex((accountId == null ? string.Empty : accountId) + "|" + (day == null ? string.Empty : day));
            int v = Convert.ToInt32(hex.Substring(0, 4), 16);
            return v % (maxSec + 1);
        }

        public static DateTime TargetAt(DateTime now, string target, int jitterSec)
        {
            TimeSpan t;
            if (!TimeSpan.TryParseExact(target, "hh\\:mm", CultureInfo.InvariantCulture, out t))
                t = new TimeSpan(0, 5, 0);
            DateTime d = now.Date.Add(t);
            return d.AddSeconds(Math.Max(0, jitterSec));
        }

        public static bool ShouldRun(DateTime now, Account account, Settings settings, AccountRunState state)
        {
            if (settings == null || account == null) return false;
            if (!settings.AutoCheckin) return false;
            if (!account.Enabled) return false;
            PlatformSetting p = settings.Platform(account.Platform);
            if (p == null || !p.Enabled) return false;

            if (now.Hour >= settings.Retry.GiveUpHour) return false;

            if (state != null)
            {
                if (state.Status == CheckinStatus.Success || state.Status == CheckinStatus.Already ||
                    state.Status == CheckinStatus.Occupied || state.Status == CheckinStatus.Auth ||
                    state.Status == CheckinStatus.GivenUp) return false;
                DateTime retry;
                if (!string.IsNullOrEmpty(state.NextRetryAt) &&
                    DateTime.TryParse(state.NextRetryAt, out retry) && now < retry) return false;
            }

            string day = DayKey(now);
            int jitter = JitterSec(account.Id, day, p.JitterSec);
            DateTime target = TargetAt(now, p.Target, jitter);
            return now >= target;
        }

        public static DateTime? NextRetry(DateTime now, int attempts, Settings settings)
        {
            if (settings == null) return null;
            int[] minutes = settings.Retry.Minutes;
            if (minutes == null || minutes.Length == 0) minutes = new int[] { 5, 15, 30, 60, 120 };
            int idx = attempts;
            if (idx < 0) idx = 0;
            if (idx >= minutes.Length) idx = minutes.Length - 1;
            DateTime next = now.AddMinutes(minutes[idx]);
            DateTime deadline = now.Date.AddHours(settings.Retry.GiveUpHour);
            if (next > deadline) next = deadline;
            if (next <= now) next = now.AddMinutes(1);
            return next;
        }

        public static bool Terminal(OutcomeKind kind)
        {
            return kind == OutcomeKind.Success || kind == OutcomeKind.SuccessUnconfirmed ||
                   kind == OutcomeKind.Already || kind == OutcomeKind.Occupied ||
                   kind == OutcomeKind.AuthRequired || kind == OutcomeKind.Permanent;
        }

        public static string StatusOf(OutcomeKind kind)
        {
            switch (kind)
            {
                case OutcomeKind.Success: return CheckinStatus.Success;
                case OutcomeKind.SuccessUnconfirmed: return CheckinStatus.Success;
                case OutcomeKind.Already: return CheckinStatus.Already;
                case OutcomeKind.Occupied: return CheckinStatus.Occupied;
                case OutcomeKind.AuthRequired: return CheckinStatus.Auth;
                case OutcomeKind.Pending: return CheckinStatus.Pending;
                case OutcomeKind.Permanent: return CheckinStatus.Failed;
                default: return CheckinStatus.Failed;
            }
        }

        public static string StateStatusOf(OutcomeKind kind)
        {
            if (kind == OutcomeKind.SuccessUnconfirmed) return CheckinStatus.Pending;
            return StatusOf(kind);
        }
    }
}
