using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class InfoHashTests
    {
        InfoHash Create()
        {
            return new InfoHash(new byte[] {
                1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20
            });
        }

        [Test]
        public void HexTest()
        {
            InfoHash hash = Create();
            string hex = hash.ToHex();
            Assert.AreEqual(40, hex.Length, "#1");
            InfoHash other = InfoHash.FromHex(hex);
            Assert.AreEqual(hash, other, "#2");
        }

        [Test]
        public void InvalidHex()
        {
            Assert.Throws<ArgumentException>(() => InfoHash.FromHex("123123123123123123123"));
        }

        [Test]
        public void NullHex()
        {
            Assert.Throws<ArgumentException>(() => InfoHash.FromHex(null));
        }
    }
}
