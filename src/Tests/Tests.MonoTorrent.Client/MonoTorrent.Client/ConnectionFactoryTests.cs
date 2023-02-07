using System;
using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class PeerConnectionFactoryTests
    {
        [Test]
        public void InvalidPort ()
        {
            Assert.IsNull (Factories.Default.CreatePeerConnection (new Uri ("tcp://127.0.1.2")));
        }
    }
}
