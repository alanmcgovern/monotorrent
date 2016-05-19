using System;
using Xunit;

namespace MonoTorrent.Common
{
    public class UriQueryBuilderTest
    {
        [Fact]
        public void AddParams()
        {
            var b = new UriQueryBuilder("http://example.com");
            b["Test"] = "2";
            b["Test"] = "7";
            Assert.Equal("7", b["Test"]);
        }

        [Fact]
        public void CaseInsensitiveTest()
        {
            var b = new UriQueryBuilder("http://www.example.com?first=1&second=2&third=4");
            Assert.True(b.Contains("FiRsT"));
            Assert.Equal(b["FiRst"], "1");
        }

        [Fact]
        public void ContainQuery()
        {
            var bld = new UriQueryBuilder("http://mytest.com/announce.aspx?key=1&foo=bar");
            Assert.True(bld.Contains("key"));
            Assert.True(bld.Contains("foo"));
            Assert.False(bld.Contains("bar"));
        }

        [Fact]
        public void TestToString()
        {
            var bld = new UriQueryBuilder("http://mytest.com/announce.aspx?key=1");
            bld.Add("key", 2);
            bld.Add("foo", 2);
            bld.Add("foo", "bar");
            Assert.Equal(new Uri("http://mytest.com/announce.aspx?key=2&foo=bar"), bld.ToUri());

            bld = new UriQueryBuilder("http://mytest.com/announce.aspx?passkey=1");
            bld.Add("key", 2);
            Assert.Equal(new Uri("http://mytest.com/announce.aspx?passkey=1&key=2"), bld.ToUri());

            bld = new UriQueryBuilder("http://mytest.com/announce.aspx");
            Assert.Equal(new Uri("http://mytest.com/announce.aspx"), bld.ToUri());

            bld = new UriQueryBuilder("http://mytest.com/announce.aspx");
            var infoHash = new byte[6] {0x01, 0x47, 0xff, 0xaa, 0xbb, 0xcc};
            bld.Add("key", UriHelper.UrlEncode(infoHash));
            Assert.Equal(new Uri("http://mytest.com/announce.aspx?key=%01G%ff%aa%bb%cc"), bld.ToUri());
        }
    }
}