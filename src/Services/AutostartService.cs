using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace AiSign.Services
{
    internal static class AutostartService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "AiSign";

        public static string ExePath
        {
            get
            {
                try
                {
                    Assembly a = Assembly.GetEntryAssembly();
                    if (a != null && !string.IsNullOrEmpty(a.Location)) return a.Location;
                }
                catch { }
                try
                {
                    using (Process p = Process.GetCurrentProcess())
                    {
                        if (p.MainModule != null) return p.MainModule.FileName;
                    }
                }
                catch { }
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AiSign.exe");
            }
        }

        public static string CommandLine
        {
            get { return "\"" + ExePath + "\" --minimized"; }
        }

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    object v = k.GetValue(ValueName);
                    return v != null && !string.IsNullOrEmpty(v.ToString());
                }
            }
            catch { return false; }
        }

        public static bool MatchesCurrent()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    object v = k.GetValue(ValueName);
                    if (v == null) return false;
                    return string.Equals(v.ToString().Trim(), CommandLine.Trim(), StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }

        public static bool SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k == null) return false;
                    if (enabled) k.SetValue(ValueName, CommandLine, RegistryValueKind.String);
                    else k.DeleteValue(ValueName, false);
                }
                return true;
            }
            catch (Exception ex)
            {
                Core.Logger.Warn("autostart", "写入自启失败: " + ex.Message);
                return false;
            }
        }

        public static void SelfHeal()
        {
            try
            {
                if (!IsEnabled() || MatchesCurrent()) return;
                SetEnabled(true);
                Core.Logger.Info("autostart", "自启路径已更新为 " + ExePath);
            }
            catch { }
        }
    }
}
