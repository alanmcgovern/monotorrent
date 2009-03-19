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
        [ExpectedException (typeof(ArgumentException))]
        public void InvalidHex()
        {
            InfoHash.FromHex("123123123123123123123");
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NullHex()
        {
            InfoHash.FromHex(null);
        }

        [Test]
        public void MagnetLink()
        {
            InfoHash hash = Create();
            string magnet = string.Format("magnet:?xt=urn:btih:{0}", hash.ToHex());
            InfoHash other = InfoHash.FromMagnetLink(magnet);
            Assert.AreEqual(hash, other, "#1");
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void InvalidMagnetLink()
        {
            InfoHash hash = Create();
            string magnet = string.Format("magnet?xt=urn:btih:{0}", hash.ToHex());
            InfoHash other = InfoHash.FromMagnetLink(magnet);
            Assert.AreEqual(hash, other, "#1");
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void InvalidMagnetLink2()
        {
            InfoHash hash = Create();
            string magnet = string.Format("magnet:?xt=urn:btih:", hash.ToHex());
            InfoHash other = InfoHash.FromMagnetLink(magnet);
            Assert.AreEqual(hash, other, "#1");
        }

        [Test]
        [ExpectedException (typeof(ArgumentException))]
        public void InvalidMagnetLink3()
        {
            InfoHash hash = Create();
            string magnet = string.Format("magnet:?xt=urn:btih:23526246235623564234365879634581726345981", hash.ToHex());
            InfoHash other = InfoHash.FromMagnetLink(magnet);
            Assert.AreEqual(hash, other, "#1");
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NullMagnetLink3()
        {
            InfoHash other = InfoHash.FromMagnetLink(null);
        }
    }
}
