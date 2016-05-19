using System.Net;
using MonoTorrent.Client;
using Xunit;

namespace MonoTorrent.Tests.Client
{
    public class BanListTests
    {
        public BanListTests()
        {
            list = new BanList();
            list.Add(new AddressRange(IPAddress.Parse("0.0.0.1"), IPAddress.Parse("0.0.0.10")));
            list.Add(new AddressRange(IPAddress.Parse("255.255.255.0"), IPAddress.Parse("255.255.255.255")));
        }

        private readonly BanList list;

        [Fact]
        public void BannedTest()
        {
            Assert.True(list.IsBanned(IPAddress.Parse("0.0.0.1")));
            Assert.True(list.IsBanned(IPAddress.Parse("0.0.0.10")));
            Assert.False(list.IsBanned(IPAddress.Parse("1.0.0.0")));
            Assert.True(list.IsBanned(IPAddress.Parse("255.255.255.0")));
            Assert.True(list.IsBanned(IPAddress.Parse("255.255.255.5")));
            Assert.True(list.IsBanned(IPAddress.Parse("255.255.255.255")));
        }

        [Fact]
        public void UnbanTest()
        {
            list.Remove(IPAddress.Parse("0.0.0.1"));
            list.Remove(IPAddress.Parse("0.0.0.3"));
            list.Remove(IPAddress.Parse("0.0.0.10"));
            list.Remove(IPAddress.Parse("255.255.255.200"));

            Assert.False(list.IsBanned(IPAddress.Parse("0.0.0.1")));
            Assert.True(list.IsBanned(IPAddress.Parse("0.0.0.2")));
            Assert.False(list.IsBanned(IPAddress.Parse("0.0.0.3")));
            Assert.True(list.IsBanned(IPAddress.Parse("0.0.0.4")));
            Assert.True(list.IsBanned(IPAddress.Parse("0.0.0.9")));
            Assert.False(list.IsBanned(IPAddress.Parse("0.0.0.10")));
            Assert.True(list.IsBanned(IPAddress.Parse("255.255.255.199")));
            Assert.False(list.IsBanned(IPAddress.Parse("255.255.255.200")));
            Assert.True(list.IsBanned(IPAddress.Parse("255.255.255.201")));
        }
    }
}