
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

        [Test, ExpectedException(typeof (FormatException))]
        public void InvalidMagnetLink3()
        {
            InfoHash hash = Create();
            string magnet = string.Format("magnet:?xt=urn:btih:", hash.ToHex());
            MagnetLink other = new MagnetLink(magnet);
            Assert.AreEqual(hash, other.InfoHash, "#1");
        }

        [Test]
        [ExpectedException (typeof(FormatException))]
        public void InvalidMagnetLink4()
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

        [Test]
        public void TrackersUrlEncodedTest()
        {
            var other = new MagnetLink("magnet:?xt=urn:ed2k:354B15E68FB8F36D7CD88FF94116CDC1" +
                        "&xl=10826029&dn=mediawiki-1.15.1.tar.gz"+
                        "&xt=urn:tree:tiger:7N5OAMRNGMSSEUE3ORHOKWN4WWIQ5X4EBOOTLJY" +
                        "&xt=urn:btih:YNCKHTQCWBTRNJIV4WNAE52SJUQCZO5C" +
                        "&tr=http%3A%2F%2Ftracker.example.org%2Fannounce.php%3Fuk%3D1111111111%26" +
                        "&tr=udp%3A%2F%2Fexample.org%3A28191" +
                        "&tr=tcp%3A%2F%2F%5B2001%3Adb8%3A85a3%3A8d3%3A1319%3A8a2e%3A370%3A7348%5D");
            Assert.IsNotNull(other.AnnounceUrls, "#1");
            Assert.IsTrue(other.AnnounceUrls.Contains("http://tracker.example.org/announce.php?uk=1111111111&"), "#2");
            Assert.IsTrue(other.AnnounceUrls.Contains("udp://example.org:28191"), "#3");
            Assert.IsTrue(other.AnnounceUrls.Contains("tcp://[2001:db8:85a3:8d3:1319:8a2e:370:7348]"), "#4");
        }

    }
}
