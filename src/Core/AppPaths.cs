using System;
using System.IO;

namespace AiSign.Core
{
    internal static class AppPaths
    {
        private static string _dataDir;

        public static string DataDir
        {
            get
            {
                if (!string.IsNullOrEmpty(_dataDir)) return _dataDir;
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                _dataDir = Path.Combine(appData, "AiSign");
                return _dataDir;
            }
        }

        public static string LogDir { get { return Path.Combine(DataDir, "logs"); } }

        public static string SettingsFile { get { return Path.Combine(DataDir, "settings.json"); } }
        public static string AccountsFile { get { return Path.Combine(DataDir, "accounts.json"); } }
        public static string SecretsFile { get { return Path.Combine(DataDir, "secrets.json"); } }
        public static string ResultsFile { get { return Path.Combine(DataDir, "results.json"); } }
        public static string StateFile { get { return Path.Combine(DataDir, "state.json"); } }

        public static void SetDataDir(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            _dataDir = Path.GetFullPath(dir);
        }

        public static void Ensure()
        {
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(LogDir);
        }
    }
}
