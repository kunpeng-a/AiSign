using System;
using System.Security.Cryptography;
using System.Text;

namespace AiSign.Core
{
    internal static class Dpapi
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("aisign-v1");

        public static string ProtectString(string plain)
        {
            if (plain == null) return null;
            byte[] data = Encoding.UTF8.GetBytes(plain);
            byte[] enc = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(enc);
        }

        public static string UnprotectString(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return null;
            try
            {
                byte[] data = Convert.FromBase64String(encoded);
                byte[] plain = ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                return null;
            }
        }

        public static byte[] Protect(byte[] data)
        {
            return ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
        }

        public static byte[] Unprotect(byte[] data)
        {
            return ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
        }
    }
}
