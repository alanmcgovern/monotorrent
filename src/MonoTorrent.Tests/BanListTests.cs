using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client;
using System.Net;

namespace MonoTorrent.Tests
{
    [TestFixture]
    public class BanListTests
    {
        BanList list;
        
        [SetUp]
        public void Setup()
        {
            list = new BanList();
            list.Add(new AddressRange(IPAddress.Parse("0.0.0.1"), IPAddress.Parse("0.0.0.10")));
        }

        [Test]
        public void BannedTest()
        {
            Assert.IsTrue(list.IsBanned(IPAddress.Parse("0.0.0.1")), "#1");
            Assert.IsTrue(list.IsBanned(IPAddress.Parse("0.0.0.10")), "#2");
            Assert.IsFalse(list.IsBanned(IPAddress.Parse("1.0.0.0")), "#3");
        }

        [Test]
        public void UnbanTest()
        {
            list.Remove(IPAddress.Parse("0.0.0.1"));
            list.Remove(IPAddress.Parse("0.0.0.3"));
            list.Remove(IPAddress.Parse("0.0.0.10"));

            Assert.IsFalse(list.IsBanned(IPAddress.Parse("0.0.0.1")), "#1");
            Assert.IsTrue(list.IsBanned(IPAddress.Parse("0.0.0.2")), "#2");
            Assert.IsFalse(list.IsBanned(IPAddress.Parse("0.0.0.3")), "#3");
            Assert.IsTrue(list.IsBanned(IPAddress.Parse("0.0.0.4")), "#4");
            Assert.IsTrue(list.IsBanned(IPAddress.Parse("0.0.0.9")), "#5");
            Assert.IsFalse(list.IsBanned(IPAddress.Parse("0.0.0.10")), "#6");
        }
    }
}
