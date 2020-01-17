using System;

using MonoTorrent.Client.Connections;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class ConnectionFactoryTests
    {
        [Test]
        public void InvalidPort ()
        {
            Assert.IsNull (ConnectionFactory.Create (new Uri ("ipv4://127.0.1.2")));
        }
    }
}
