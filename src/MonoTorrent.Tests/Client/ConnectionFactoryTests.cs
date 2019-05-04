using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client;
using System.Net;
using System.Net.Sockets;
using MonoTorrent.Client.Connections;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class ConnectionFactoryTests
    {
        [Test]
        public void InvalidPort ()
        {
            Assert.IsNull (ConnectionFactory.Create(new Uri ("tcp://127.0.1.2")));
        }
    }
}
