using System;
using System.IO;
using System.Text;

namespace AiSign.Core
{
    internal static class AtomicFile
    {
        public static void WriteAllText(string path, string text)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                try { File.Replace(tmp, path, null); }
                catch (PlatformNotSupportedException) { File.Copy(tmp, path, true); File.Delete(tmp); }
                catch (IOException) { File.Copy(tmp, path, true); File.Delete(tmp); }
            }
            else
            {
                File.Move(tmp, path);
            }
        }

        public static string ReadAllText(string path)
        {
            if (!File.Exists(path)) return null;
            return File.ReadAllText(path, Encoding.UTF8);
        }

        public static void BackupCorrupt(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                string bak = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Move(path, bak);
            }
            catch { }
        }
    }

    internal static class JsonStore
    {
        private static readonly object Lock = new object();

        public static T Load<T>(string path, Func<T> fallback) where T : class
        {
            lock (Lock)
            {
                try
                {
                    string text = AtomicFile.ReadAllText(path);
                    if (string.IsNullOrEmpty(text)) return fallback();
                    T value = JsonUtil.Deserialize<T>(text);
                    return value == null ? fallback() : value;
                }
                catch
                {
                    AtomicFile.BackupCorrupt(path);
                    return fallback();
                }
            }
        }

        public static void Save<T>(string path, T value)
        {
            lock (Lock)
            {
                AtomicFile.WriteAllText(path, JsonUtil.Serialize(value));
            }
        }
    }
}
