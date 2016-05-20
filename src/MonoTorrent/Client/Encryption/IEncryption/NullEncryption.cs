using System;

namespace MonoTorrent.Client.Encryption
{
    /// <summary>
    ///     Plaintext "encryption"
    /// </summary>
    public class PlainTextEncryption : IEncryption
    {
        public void Decrypt(byte[] buffer)
        {
            // Nothing
        }

        public void Decrypt(byte[] buffer, int offset, int count)
        {
            // Nothing
        }

        public void Decrypt(byte[] src, int srcOffset, byte[] dest, int destOffset, int count)
        {
            Encrypt(src, srcOffset, dest, destOffset, count);
        }

        public void Encrypt(byte[] buffer)
        {
            // Nothing
        }

        public void Encrypt(byte[] buffer, int offset, int count)
        {
            // Nothing
        }

        public void Encrypt(byte[] src, int srcOffset, byte[] dest, int destOffset, int count)
        {
            Buffer.BlockCopy(src, srcOffset, dest, destOffset, count);
        }
    }
}