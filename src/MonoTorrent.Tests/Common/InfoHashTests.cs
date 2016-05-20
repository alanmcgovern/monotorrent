using System;
using Xunit;

namespace MonoTorrent.Tests.Common
{
    public class InfoHashTests
    {
        private InfoHash Create()
        {
            return new InfoHash(new byte[]
            {
                1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20
            });
        }

        [Fact]
        public void HexTest()
        {
            var hash = Create();
            var hex = hash.ToHex();
            Assert.Equal(40, hex.Length);
            var other = InfoHash.FromHex(hex);
            Assert.Equal(hash, other);
        }

        [Fact]
        public void InvalidHex()
        {
            Assert.Throws<ArgumentException>(() => InfoHash.FromHex("123123123123123123123"));
        }

        [Fact]
        public void NullHex()
        {
            Assert.Throws<ArgumentException>(() => InfoHash.FromHex(null));
        }
    }
}