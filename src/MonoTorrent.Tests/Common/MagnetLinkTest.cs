using System;
using System.Text;
using Xunit;

namespace MonoTorrent.Tests.Common
{
    public class MagnetLinkTest
    {
        /*static void Main(string[] args)
        {
            MagnetLinkTest t = new MagnetLinkTest();
            t.InfoHashTest();
            t.MagnetLink();
        }*/

        private InfoHash Create()
        {
            return new InfoHash(new byte[]
            {
                1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20
            });
        }

        [Fact]
        public void InfoHashTest()
        {
            var link = new MagnetLink("magnet:?xt.1=urn:sha1:YNCKHTQCWBTRNJIV4WNAE52SJUQCZO5C");
            Assert.Equal(InfoHash.FromBase32("YNCKHTQCWBTRNJIV4WNAE52SJUQCZO5C"), link.InfoHash);

            //base32
            var initial = new InfoHash(Encoding.ASCII.GetBytes("foobafoobafoobafooba"));
            link = new MagnetLink("magnet:?xt=urn:sha1:MZXW6YTBMZXW6YTBMZXW6YTBMZXW6YTB");
            Assert.Equal(initial, link.InfoHash);

            //base40 = hex
            var hash = Create();
            var hex = hash.ToHex();
            link = new MagnetLink("magnet:?xt=urn:btih:" + hex);
            Assert.Equal(hash, link.InfoHash);
        }

        [Fact]
        public void InvalidMagnetLink()
        {
            var hash = Create();
            var magnet = string.Format("magnet?xt=urn:btih:{0}", hash.ToHex());
            MagnetLink other = null;
            Assert.Throws<FormatException>(() => other = new MagnetLink(magnet));
            Assert.Equal(hash, other.InfoHash);
        }

        [Fact]
        public void InvalidMagnetLink3()
        {
            var hash = Create();
            var magnet = string.Format("magnet:?xt=urn:btih:", hash.ToHex());
            MagnetLink other = null;
            Assert.Throws<FormatException>(() => other = new MagnetLink(magnet));
            Assert.Equal(hash, other.InfoHash);
        }

        [Fact]
        public void InvalidMagnetLink4()
        {
            var hash = Create();
            var magnet = string.Format("magnet:?xt=urn:btih:23526246235623564234365879634581726345981", hash.ToHex());
            MagnetLink other = null;
            Assert.Throws<FormatException>(() => other = new MagnetLink(magnet));
            Assert.Equal(hash, other.InfoHash);
        }

        [Fact]
        public void MagnetLink()
        {
            var hash = Create();
            var magnet = string.Format("magnet:?xt=urn:btih:{0}", hash.ToHex());
            var other = new MagnetLink(magnet);
            Assert.Equal(hash, other.InfoHash);
        }

        [Fact]
        public void NameTest()
        {
            var other = new MagnetLink("magnet:?dn=foo");
            Assert.NotNull(other.Name);
            Assert.Equal("foo", other.Name);
        }

        [Fact]
        public void NullMagnetLink()
        {
            Assert.Throws<FormatException>(() => new MagnetLink(null));
        }

        [Fact]
        public void TrackersTest()
        {
            var other = new MagnetLink("magnet:?tr=http://example.com/announce&tr.2=http://example.com/announce2");
            Assert.NotNull(other.AnnounceUrls);
            Assert.True(other.AnnounceUrls.Contains("http://example.com/announce"));
            Assert.True(other.AnnounceUrls.Contains("http://example.com/announce2"));
        }

        [Fact]
        public void TrackersUrlEncodedTest()
        {
            var other = new MagnetLink("magnet:?xt=urn:ed2k:354B15E68FB8F36D7CD88FF94116CDC1" +
                                       "&xl=10826029&dn=mediawiki-1.15.1.tar.gz" +
                                       "&xt=urn:tree:tiger:7N5OAMRNGMSSEUE3ORHOKWN4WWIQ5X4EBOOTLJY" +
                                       "&xt=urn:btih:YNCKHTQCWBTRNJIV4WNAE52SJUQCZO5C" +
                                       "&tr=http%3A%2F%2Ftracker.example.org%2Fannounce.php%3Fuk%3D1111111111%26" +
                                       "&tr=udp%3A%2F%2Fexample.org%3A28191" +
                                       "&tr=tcp%3A%2F%2F%5B2001%3Adb8%3A85a3%3A8d3%3A1319%3A8a2e%3A370%3A7348%5D");
            Assert.NotNull(other.AnnounceUrls);
            Assert.True(other.AnnounceUrls.Contains("http://tracker.example.org/announce.php?uk=1111111111&"));
            Assert.True(other.AnnounceUrls.Contains("udp://example.org:28191"));
            Assert.True(other.AnnounceUrls.Contains("tcp://[2001:db8:85a3:8d3:1319:8a2e:370:7348]"));
        }
    }
}