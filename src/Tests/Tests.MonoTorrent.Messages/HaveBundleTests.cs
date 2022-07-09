using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

namespace MonoTorrent.Messages.Peer
{
    [TestFixture]
    public class HaveBundleTests
    {
        [Test]
        public void HaveBundleIsCached ()
        {
            (var msg, var releaser) = PeerMessage.Rent<HaveBundle> ();
            releaser.Dispose ();
            (var msg2, var releaser2) = PeerMessage.Rent<HaveBundle> ();
            releaser2.Dispose ();
            Assert.AreSame (msg, msg2);
        }
    }
}
