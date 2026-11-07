using System;
using System.Security.Cryptography;
using System.Text;

namespace AiSign.Crypto
{
    internal static class TraeCipher
    {
        private static readonly byte[] Ure = new byte[] { 82,9,106,213,48,54,165,56,191,64,163,158,129,243,215,251,124,227,57,130,155,47,255,135,52,142,67,68,196,222,233,203,84,123,148,50,166,194,35,61,238,76,149,11,66,250,195,78,8,46,161,102,40,217,36,178,118,91,162,73,109,139,209,37 };
        private static readonly byte[] Dre = new byte[] { 31,221,168,51,136,7,199,49,177,18,16,89,39,128,236,95,96,81,127,169,25,181,74,13,45,229,122,159,147,201,156,239,160,224,59,77,174,42,245,176,200,235,187,60,131,83,153,97,23,43,4,126,186,119,214,38,225,105,20,99,85,33,12,125 };

        public static string Decrypt(string enc)
        {
            byte[] t = Convert.FromBase64String(enc);
            if (t.Length < 38) throw new InvalidOperationException("iCube 密文长度非法");

            byte[] seed = new byte[32];
            Buffer.BlockCopy(t, 6, seed, 0, 32);
            byte[] aesKey;
            byte[] iv;
            Derive(seed, out aesKey, out iv);

            byte[] ct = new byte[t.Length - 38];
            Buffer.BlockCopy(t, 38, ct, 0, ct.Length);

            byte[] dec;
            using (Aes aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.KeySize = 128;
                aes.Key = aesKey;
                aes.IV = iv;
                using (ICryptoTransform d = aes.CreateDecryptor())
                    dec = d.TransformFinalBlock(ct, 0, ct.Length);
            }
            if (dec.Length <= 64) throw new InvalidOperationException("iCube 明文长度非法");
            return Encoding.UTF8.GetString(dec, 64, dec.Length - 64);
        }

        public static void Derive(byte[] seed, out byte[] aesKey, out byte[] iv)
        {
            if (seed == null || seed.Length != 32) throw new ArgumentException("seed 必须为 32 字节");
            byte[] sha;
            using (SHA512 h = SHA512.Create()) sha = h.ComputeHash(seed);

            byte[] xor = new byte[64];
            for (int i = 0; i < 64; i++) xor[i] = (byte)(Ure[i] ^ Dre[i]);

            byte[] comb = new byte[128];
            Buffer.BlockCopy(sha, 0, comb, 0, 64);
            Buffer.BlockCopy(xor, 0, comb, 64, 64);

            byte[] hash;
            using (SHA512 h2 = SHA512.Create()) hash = h2.ComputeHash(comb);
            aesKey = new byte[16];
            Buffer.BlockCopy(hash, 0, aesKey, 0, 16);
            iv = new byte[16];
            Buffer.BlockCopy(hash, 16, iv, 0, 16);
        }

        public static bool LooksLikeCipher(string text)
        {
            if (string.IsNullOrEmpty(text) || text.StartsWith("eyJ", StringComparison.Ordinal)) return false;
            try
            {
                byte[] t = Convert.FromBase64String(text);
                return t.Length >= 38;
            }
            catch { return false; }
        }
    }
}
