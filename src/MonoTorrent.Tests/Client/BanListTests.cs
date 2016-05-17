using System.Net;
using Xunit;

namespace MonoTorrent.Client
{
    public class BanListTests
    {
        BanList list;
        
        public BanListTests()
        {
            list = new BanList();
            list.Add(new AddressRange(IPAddress.Parse("0.0.0.1"), IPAddress.Parse("0.0.0.10")));
            list.Add(new AddressRange(IPAddress.Parse("255.255.255.0"), IPAddress.Parse("255.255.255.255")));
        }

        [Fact]
        public void BannedTest()
        {
            Assert.True(list.IsBanned(IPAddress.Parse("0.0.0.1")), "#1");
            Assert.True(list.IsBanned(IPAddress.Parse("0.0.0.10")), "#2");
            Assert.False(list.IsBanned(IPAddress.Parse("1.0.0.0")), "#3");
            Assert.True(list.IsBanned(IPAddress.Parse("255.255.255.0")), "#4");
            Assert.True(list.IsBanned(IPAddress.Parse("255.255.255.5")), "#5");
            Assert.True(list.IsBanned(IPAddress.Parse("255.255.255.255")), "#6");
        }

        [Fact]
        public void UnbanTest()
        {
            list.Remove(IPAddress.Parse("0.0.0.1"));
            list.Remove(IPAddress.Parse("0.0.0.3"));
            list.Remove(IPAddress.Parse("0.0.0.10"));
            list.Remove(IPAddress.Parse("255.255.255.200"));

            Assert.False(list.IsBanned(IPAddress.Parse("0.0.0.1")), "#1");
            Assert.True(list.IsBanned(IPAddress.Parse("0.0.0.2")), "#2");
            Assert.False(list.IsBanned(IPAddress.Parse("0.0.0.3")), "#3");
            Assert.True(list.IsBanned(IPAddress.Parse("0.0.0.4")), "#4");
            Assert.True(list.IsBanned(IPAddress.Parse("0.0.0.9")), "#5");
            Assert.False(list.IsBanned(IPAddress.Parse("0.0.0.10")), "#6");
            Assert.True(list.IsBanned(IPAddress.Parse("255.255.255.199")), "#7");
            Assert.False(list.IsBanned(IPAddress.Parse("255.255.255.200")), "#8");
            Assert.True(list.IsBanned(IPAddress.Parse("255.255.255.201")), "#9");
        }
    }
}
