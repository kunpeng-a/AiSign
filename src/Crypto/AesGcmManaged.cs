using System;
using System.Security.Cryptography;

namespace AiSign.Crypto
{
    // .NET Framework 4.8 没有 AesGcm，这里实现解密所需的 GCM（96 位 IV、16 字节 tag、无 AAD）
    internal static class AesGcmManaged
    {
        public static byte[] Decrypt(byte[] key, byte[] iv, byte[] tag, byte[] cipherText)
        {
            if (key == null || iv == null || tag == null || cipherText == null) return null;
            if (key.Length != 32 || iv.Length != 12 || tag.Length != 16) return null;

            using (Aes aes = Aes.Create())
            {
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                aes.KeySize = 256;
                aes.Key = key;
                using (ICryptoTransform enc = aes.CreateEncryptor())
                {
                    byte[] h = new byte[16];
                    enc.TransformBlock(h, 0, 16, h, 0);

                    byte[] j0 = new byte[16];
                    Buffer.BlockCopy(iv, 0, j0, 0, 12);
                    j0[15] = 1;

                    byte[] plain = new byte[cipherText.Length];
                    CtrXor(enc, j0, cipherText, plain);

                    byte[] s = Ghash(h, null, cipherText);

                    byte[] ek0 = new byte[16];
                    enc.TransformBlock(j0, 0, 16, ek0, 0);
                    Xor(ek0, s);
                    byte diff = 0;
                    for (int i = 0; i < 16; i++) diff |= (byte)(ek0[i] ^ tag[i]);
                    if (diff != 0) return null;
                    return plain;
                }
            }
        }

        private static void CtrXor(ICryptoTransform enc, byte[] j0, byte[] input, byte[] output)
        {
            byte[] counter = (byte[])j0.Clone();
            byte[] stream = new byte[16];
            for (int off = 0; off < input.Length; off += 16)
            {
                Inc32(counter);
                enc.TransformBlock(counter, 0, 16, stream, 0);
                int n = Math.Min(16, input.Length - off);
                for (int i = 0; i < n; i++) output[off + i] = (byte)(input[off + i] ^ stream[i]);
            }
        }

        private static void Inc32(byte[] block)
        {
            for (int i = 15; i >= 12; i--)
            {
                block[i] = (byte)(block[i] + 1);
                if (block[i] != 0) break;
            }
        }

        private static byte[] Ghash(byte[] h, byte[] aad, byte[] cipher)
        {
            byte[] y = new byte[16];
            if (aad != null && aad.Length > 0)
            {
                byte[] ab = new byte[16];
                int off = 0;
                while (off < aad.Length)
                {
                    int n = Math.Min(16, aad.Length - off);
                    Array.Clear(ab, 0, 16);
                    Buffer.BlockCopy(aad, off, ab, 0, n);
                    Xor(y, ab);
                    byte[] m = GfMul(y, h);
                    Buffer.BlockCopy(m, 0, y, 0, 16);
                    off += 16;
                }
            }
            byte[] cb = new byte[16];
            int co = 0;
            while (co < cipher.Length)
            {
                int n = Math.Min(16, cipher.Length - co);
                Array.Clear(cb, 0, 16);
                Buffer.BlockCopy(cipher, co, cb, 0, n);
                Xor(y, cb);
                byte[] m = GfMul(y, h);
                Buffer.BlockCopy(m, 0, y, 0, 16);
                co += 16;
            }
            byte[] lenBlock = new byte[16];
            WriteULongBE(lenBlock, 0, aad != null ? (ulong)aad.Length * 8 : 0);
            WriteULongBE(lenBlock, 8, (ulong)cipher.Length * 8);
            Xor(y, lenBlock);
            return GfMul(y, h);
        }

        private static byte[] GfMul(byte[] x, byte[] h)
        {
            byte[] z = new byte[16];
            byte[] v = new byte[16];
            Buffer.BlockCopy(h, 0, v, 0, 16);
            for (int i = 0; i < 128; i++)
            {
                if ((x[i >> 3] & (byte)(0x80 >> (i & 7))) != 0) Xor(z, v);
                int lsb = v[15] & 1;
                RightShift1(v);
                if (lsb != 0) v[0] ^= 0xE1;
            }
            return z;
        }

        private static void RightShift1(byte[] b)
        {
            for (int i = 15; i > 0; i--)
                b[i] = (byte)(((b[i] & 0xFE) >> 1) | ((b[i - 1] & 1) << 7));
            b[0] = (byte)(b[0] >> 1);
        }

        private static void Xor(byte[] dst, byte[] src)
        {
            for (int i = 0; i < 16; i++) dst[i] ^= src[i];
        }

        private static void WriteULongBE(byte[] b, int off, ulong v)
        {
            for (int i = 0; i < 8; i++) b[off + i] = (byte)(v >> (56 - i * 8));
        }
    }
}
