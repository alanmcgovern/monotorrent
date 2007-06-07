using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using System.Security.Cryptography;

namespace MonoTorrent.Tests.Encryption.Tests
{
    [TestFixture]
    public class RC4Test
    {
        [Test]
        public void CryptTest()
        {
            MonoTorrent.Client.Encryption.RC4 rc4a;
            MonoTorrent.Client.Encryption.RC4 rc4b;

            RandomNumberGenerator random = RandomNumberGenerator.Create();
            Random random2 = new Random();

            byte[] key = new byte[20];
            random.GetBytes(key);

            rc4a = new MonoTorrent.Client.Encryption.RC4(key);
            rc4b = new MonoTorrent.Client.Encryption.RC4(key);

            for (int i = 0; i < 100; i++)
            {
                byte[] toEncrypt = new byte[random2.Next(1000)];
                byte[] encrypted = rc4b.DoCrypt(rc4a.DoCrypt(toEncrypt));

                Assert.AreEqual(toEncrypt.Length, encrypted.Length);
                for (int j = 0; j < toEncrypt.Length; j++)
                {
                    Assert.AreEqual(toEncrypt[j], encrypted[j]);
                }
            }
        }

        [Test]
        public void InPlaceCryptTest()
        {
            MonoTorrent.Client.Encryption.RC4 rc4a;
            MonoTorrent.Client.Encryption.RC4 rc4b;

            RandomNumberGenerator random = RandomNumberGenerator.Create();
            Random random2 = new Random();

            byte[] key = new byte[20];
            random.GetBytes(key);

            rc4a = new MonoTorrent.Client.Encryption.RC4(key);
            rc4b = new MonoTorrent.Client.Encryption.RC4(key);

            for (int i = 0; i < 100; i++)
            {
                byte[] toEncrypt = new byte[random2.Next(1000)];
                byte[] encrypted = new byte[toEncrypt.Length];
                Array.Copy(toEncrypt, encrypted, toEncrypt.Length);

                int offset = 0;
                while (offset < toEncrypt.Length)
                {
                    int count = random2.Next(1, toEncrypt.Length - offset);
                    rc4a.InPlaceCrypt(encrypted, offset, count);

                    offset += count;                   
                }

                offset = 0;
                while (offset < toEncrypt.Length)
                {
                    int count = random2.Next(1, toEncrypt.Length - offset);
                    rc4b.InPlaceCrypt(encrypted, offset, count);
                    offset += count;
                }

                for (int j = 0; j < toEncrypt.Length; j++)
                {
                    Assert.AreEqual(toEncrypt[j], encrypted[j]);
                }
            }
        }
    }
}
