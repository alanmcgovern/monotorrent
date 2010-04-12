
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class MagnetLinkTest
    {

        /*static void Main(string[] args)
        {
            MagnetLinkTest t = new MagnetLinkTest();
            t.InfoHashTest();
            t.MagnetLink();
        }*/
        InfoHash Create()
        {
            return new InfoHash(new byte[] {
                1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20
            });
        }

        [Test]
        public void InfoHashTest()
        {
            MagnetLink link = new MagnetLink("magnet:?xt.1=urn:sha1:YNCKHTQCWBTRNJIV4WNAE52SJUQCZO5C");
            Assert.AreEqual(InfoHash.FromBase32 ("YNCKHTQCWBTRNJIV4WNAE52SJUQCZO5C"), link.InfoHash , "#1");

            //base32
            InfoHash initial = new InfoHash (System.Text.Encoding.ASCII.GetBytes("foobafoobafoobafooba"));
            link = new MagnetLink("magnet:?xt=urn:sha1:MZXW6YTBMZXW6YTBMZXW6YTBMZXW6YTB");
            Assert.AreEqual(initial, link.InfoHash , "#2");

            //base40 = hex
            InfoHash hash = Create();
            string hex = hash.ToHex();
            link = new MagnetLink("magnet:?xt=urn:btih:" + hex);
            Assert.AreEqual(hash, link.InfoHash , "#3");

        }

        [Test]
        public void MagnetLink()
        {
            InfoHash hash = Create();
            string magnet = string.Format("magnet:?xt=urn:btih:{0}", hash.ToHex());
            MagnetLink other = new MagnetLink(magnet);
            Assert.AreEqual(hash, other.InfoHash, "#1");
        }

        [Test]
        [ExpectedException(typeof(FormatException))]
        public void InvalidMagnetLink()
        {
            InfoHash hash = Create();
            string magnet = string.Format("magnet?xt=urn:btih:{0}", hash.ToHex());
            MagnetLink other = new MagnetLink(magnet);
            Assert.AreEqual(hash, other.InfoHash, "#1");
        }

        [Test]
        [ExpectedException(typeof(FormatException))]
        public void InvalidMagnetLink2()
        {
            InfoHash hash = Create();
            string magnet = string.Format("magnet:?xt=urn:btih:", hash.ToHex());
            MagnetLink other = new MagnetLink(magnet);
            Assert.AreEqual(hash, other.InfoHash, "#1");
        }

        [Test]
        [ExpectedException (typeof(FormatException))]
        public void InvalidMagnetLink3()
        {
            InfoHash hash = Create();
            string magnet = string.Format("magnet:?xt=urn:btih:23526246235623564234365879634581726345981", hash.ToHex());
            MagnetLink other = new MagnetLink(magnet);
            Assert.AreEqual(hash, other.InfoHash, "#1");
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NullMagnetLink()
        {
            new MagnetLink(null);
        }

        [Test]
        public void TrackersTest()
        {
            MagnetLink other = new MagnetLink("magnet:?tr=http://example.com/announce&tr.2=http://example.com/announce2");
            Assert.IsNotNull (other.AnnounceUrls, "#1");
            Assert.IsTrue (other.AnnounceUrls.Contains ("http://example.com/announce"), "#2");
            Assert.IsTrue (other.AnnounceUrls.Contains ("http://example.com/announce2"), "#3");
        }

        [Test]
        public void NameTest()
        {
            MagnetLink other = new MagnetLink("magnet:?dn=foo");
            Assert.IsNotNull (other.Name, "#1");
            Assert.AreEqual ("foo", other.Name, "#2");
        }
    }
}
