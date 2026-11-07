using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace AiSign.Core
{
    internal static class Logger
    {
        private static readonly object Lock = new object();
        private static readonly HashSet<string> SeenOnce = new HashSet<string>();
        private const long MaxBytes = 5 * 1024 * 1024;

        public static void Info(string module, string message) { Write("INFO", module, message, null); }
        public static void Warn(string module, string message) { Write("WARN", module, message, null); }
        public static void Error(string module, string message) { Write("ERROR", module, message, null); }
        public static void Error(string module, string message, Exception ex) { Write("ERROR", module, message, ex); }

        public static void Once(string key, string module, string message)
        {
            lock (Lock)
            {
                if (SeenOnce.Contains(key)) return;
                SeenOnce.Add(key);
            }
            Warn(module, message);
        }

        public static string Redact(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (value.Length <= 10) return "***";
            return value.Substring(0, 4) + "..." + value.Substring(value.Length - 4);
        }

        private static void Write(string level, string module, string message, Exception ex)
        {
            try
            {
                AppPaths.Ensure();
                string day = DateTime.Now.ToString("yyyyMMdd");
                string path = Path.Combine(AppPaths.LogDir, "app-" + day + ".log");
                StringBuilder line = new StringBuilder();
                line.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                line.Append(' ');
                line.Append(level);
                line.Append(" [");
                line.Append(module == null ? "-" : module);
                line.Append("] ");
                line.Append(RedactText(message));
                if (ex != null)
                {
                    line.Append(" | ");
                    line.Append(ex.GetType().Name);
                    line.Append(": ");
                    line.Append(RedactText(ex.Message));
                }
                line.AppendLine();

                lock (Lock)
                {
                    FileInfo fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > MaxBytes)
                    {
                        string old = path + ".1";
                        try { if (File.Exists(old)) File.Delete(old); File.Move(path, old); }
                        catch { }
                    }
                    File.AppendAllText(path, line.ToString(), new UTF8Encoding(false));
                }
            }
            catch { }
        }

        private static string RedactText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string s = text;
            s = ReplaceToken(s, "Bearer ");
            s = ReplaceToken(s, "Cloud-IDE-JWT ");
            s = ReplaceToken(s, "X-Refresh-Token: ");
            s = ReplaceToken(s, "authorization: ");
            return s;
        }

        private static string ReplaceToken(string text, string prefix)
        {
            int idx = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return text;
            int start = idx + prefix.Length;
            int end = start;
            while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '"' && text[end] != ',') end++;
            if (end <= start) return text;
            string token = text.Substring(start, end - start);
            string masked = token.Length <= 10 ? "***" : token.Substring(0, 4) + "..." + token.Substring(token.Length - 4);
            return text.Substring(0, start) + masked + text.Substring(end);
        }

        public static void Cleanup(int days)
        {
            try
            {
                if (!Directory.Exists(AppPaths.LogDir)) return;
                DateTime cutoff = DateTime.Now.Date.AddDays(-days);
                foreach (string file in Directory.GetFiles(AppPaths.LogDir, "app-*.log*"))
                {
                    try
                    {
                        if (File.GetLastWriteTime(file) < cutoff) File.Delete(file);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
