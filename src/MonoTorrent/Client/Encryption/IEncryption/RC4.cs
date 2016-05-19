using System.Security.Cryptography;

namespace MonoTorrent.Client.Encryption
{
    /// <summary>
    ///     RC4 encryption
    /// </summary>
    public class RC4 : IEncryption
    {
        private static readonly RandomNumberGenerator random = new RNGCryptoServiceProvider();

        private readonly byte[] S;
        private int x;
        private int y;

        public RC4(byte[] key)
        {
            S = new byte[256];
            for (var i = 0; i < S.Length; i++)
                S[i] = (byte) i;

            byte c;

            for (var i = 0; i <= 255; i++)
            {
                x = (x + S[i] + key[i%key.Length])%256;
                c = S[x];
                S[x] = S[i];
                S[i] = c;
            }

            x = 0;

            var wasteBuffer = new byte[1024];
            random.GetBytes(wasteBuffer);
            Encrypt(wasteBuffer);
        }

        public void Decrypt(byte[] buffer)
        {
            Encrypt(buffer, 0, buffer, 0, buffer.Length);
        }

        public void Decrypt(byte[] buffer, int offset, int count)
        {
            Decrypt(buffer, offset, buffer, offset, count);
        }

        public void Decrypt(byte[] src, int srcOffset, byte[] dest, int destOffset, int count)
        {
            Encrypt(src, srcOffset, dest, destOffset, count);
        }

        public void Encrypt(byte[] buffer)
        {
            Encrypt(buffer, 0, buffer, 0, buffer.Length);
        }

        public void Encrypt(byte[] buffer, int offset, int count)
        {
            Encrypt(buffer, offset, buffer, offset, count);
        }

        public void Encrypt(byte[] src, int srcOffset, byte[] dest, int destOffset, int count)
        {
            byte c;
            for (var i = 0; i < count; i++)
            {
                x = (x + 1) & 0xFF;
                y = (y + S[x]) & 0xFF;

                c = S[y];
                S[y] = S[x];
                S[x] = c;

                dest[i + destOffset] = (byte) (src[i + srcOffset] ^ S[(S[x] + S[y]) & 0xFF]);
            }
        }
    }
}