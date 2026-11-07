using System;
using System.Security.Cryptography;
using System.Text;

namespace AiSign.Core
{
    internal static class Hash
    {
        public static string Sha256Hex(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] data = Encoding.UTF8.GetBytes(text == null ? string.Empty : text);
                byte[] hash = sha.ComputeHash(data);
                return Hex(hash);
            }
        }

        public static string Hex(byte[] bytes)
        {
            if (bytes == null) return string.Empty;
            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }

        public static string RandomHex(int bytes)
        {
            byte[] buf = new byte[bytes];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(buf);
            }
            return Hex(buf);
        }

        public static string AccountId(string platform, string identity)
        {
            string id = Hash.Sha256Hex(platform + "|" + (identity == null ? string.Empty : identity));
            return platform + ":" + id.Substring(0, 16);
        }

        public static string Mask(string value, int head, int tail)
        {
            if (string.IsNullOrEmpty(value)) return "--";
            if (value.Length <= head + tail) return new string('*', Math.Min(8, value.Length));
            return value.Substring(0, head) + "..." + value.Substring(value.Length - tail);
        }
    }
}
