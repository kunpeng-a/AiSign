using System;
using AiSign.Core;

namespace AiSign.Stores
{
    internal sealed class SettingsStore
    {
        private readonly object _lock = new object();
        private Settings _settings;

        public SettingsStore()
        {
            _settings = JsonStore.Load<Settings>(AppPaths.SettingsFile, Settings.CreateDefault);
            Normalize(_settings);
        }

        public Settings Current
        {
            get { lock (_lock) return _settings; }
        }

        public void Save()
        {
            lock (_lock) JsonStore.Save(AppPaths.SettingsFile, _settings);
        }

        public void Replace(Settings settings)
        {
            if (settings == null) return;
            Normalize(settings);
            lock (_lock)
            {
                _settings = settings;
                JsonStore.Save(AppPaths.SettingsFile, _settings);
            }
        }

        private static void Normalize(Settings s)
        {
            if (s == null) return;
            if (s.Platforms == null) s.Platforms = new System.Collections.Generic.Dictionary<string, PlatformSetting>();
            if (s.Retry == null) s.Retry = new RetrySetting();
            if (s.Retry.Minutes == null || s.Retry.Minutes.Length == 0) s.Retry.Minutes = new int[] { 5, 15, 30, 60, 120 };
            if (s.Notify == null) s.Notify = new NotifySetting();
            if (s.Network == null) s.Network = new NetworkSetting();
            if (s.Window == null) s.Window = new WindowSetting();
            if (s.Advanced == null) s.Advanced = new AdvancedSetting();
            if (s.MaxConcurrency < 1) s.MaxConcurrency = 1;
            if (s.MaxConcurrency > 4) s.MaxConcurrency = 4;
            if (s.AccountGapMs < 0) s.AccountGapMs = 0;
            if (string.IsNullOrEmpty(s.Theme)) s.Theme = "dark";

            string[] platforms = PlatformIds.All();
            for (int i = 0; i < platforms.Length; i++)
            {
                PlatformSetting p;
                if (!s.Platforms.TryGetValue(platforms[i], out p) || p == null)
                {
                    p = new PlatformSetting();
                    if (platforms[i] == PlatformIds.Qoder) { p.Target = "10:05"; p.JitterSec = 90; }
                    s.Platforms[platforms[i]] = p;
                }
                if (string.IsNullOrEmpty(p.Target)) p.Target = platforms[i] == PlatformIds.Qoder ? "10:05" : "00:05";
                if (p.JitterSec < 0) p.JitterSec = 0;
            }
        }
    }
}
