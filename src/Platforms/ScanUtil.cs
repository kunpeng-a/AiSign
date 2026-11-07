using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AiSign.Platforms
{
    internal static class ScanUtil
    {
        public static Dictionary<string, object> ReadJson(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                return Core.JsonUtil.ParseObject(File.ReadAllText(path, Encoding.UTF8));
            }
            catch { return null; }
        }

        public static List<string> FindAuthDirs(string root, string[] nameKeys, int maxDepth)
        {
            List<string> result = new List<string>();
            try
            {
                if (!Directory.Exists(root)) return result;
                foreach (string top in Directory.GetDirectories(root))
                {
                    string name = Path.GetFileName(top);
                    if (!Match(name, nameKeys)) continue;
                    if (HasInfoFile(top)) result.Add(top);
                    Walk(top, nameKeys, maxDepth, 1, result);
                }
            }
            catch { }
            return result;
        }

        private static void Walk(string dir, string[] keys, int maxDepth, int depth, List<string> result)
        {
            if (depth > maxDepth) return;
            try
            {
                string name = Path.GetFileName(dir);
                if (Match(name, keys) && HasInfoFile(dir)) result.Add(dir);
                foreach (string sub in Directory.GetDirectories(dir))
                {
                    string n = Path.GetFileName(sub);
                    if (string.Equals(n, "auth", StringComparison.OrdinalIgnoreCase) && HasInfoFile(sub))
                    {
                        result.Add(sub);
                        continue;
                    }
                    Walk(sub, keys, maxDepth, depth + 1, result);
                }
            }
            catch { }
        }

        private static bool Match(string name, string[] keys)
        {
            if (string.IsNullOrEmpty(name) || keys == null) return false;
            for (int i = 0; i < keys.Length; i++)
                if (name.IndexOf(keys[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static bool HasInfoFile(string dir)
        {
            try
            {
                if (Directory.GetFiles(dir, "*.info").Length > 0) return true;
            }
            catch { }
            return false;
        }
    }

    internal static class JwtUtil
    {
        public static string PayloadJson(string jwt)
        {
            if (string.IsNullOrEmpty(jwt)) return null;
            string[] parts = jwt.Split('.');
            if (parts.Length < 2) return null;
            return Base64UrlDecode(parts[1]);
        }

        public static string Field(string jwt, string key)
        {
            string json = PayloadJson(jwt);
            if (json == null) return null;
            Dictionary<string, object> d = Core.JsonUtil.ParseObject(json);
            if (d == null) return null;
            object v;
            if (d.TryGetValue(key, out v) && v != null) return v.ToString();
            return null;
        }

        public static string Sub(string jwt)
        {
            string sub = Field(jwt, "sub");
            if (!string.IsNullOrEmpty(sub)) return sub;
            return Field(jwt, "uid");
        }

        public static string Base64UrlDecode(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            try
            {
                string v = s.Replace('-', '+').Replace('_', '/');
                switch (v.Length % 4)
                {
                    case 2: v += "=="; break;
                    case 3: v += "="; break;
                    case 1: v += "==="; break;
                }
                return Encoding.UTF8.GetString(Convert.FromBase64String(v));
            }
            catch { return null; }
        }

        public static long IsoToUnixMs(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return 0;
            DateTimeOffset dto;
            if (DateTimeOffset.TryParse(iso, out dto)) return dto.ToUnixTimeMilliseconds();
            return 0;
        }
    }
}
