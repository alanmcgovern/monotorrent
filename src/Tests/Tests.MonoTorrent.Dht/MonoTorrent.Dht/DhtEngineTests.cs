using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

namespace MonoTorrent.Dht
{
    [TestFixture]
    public class DhtEngineTests
    {
        [Test]
        public void AddRawNodesBeforeStarting ()
        {
            int count = 0;
            var engine = new DhtEngine ();
            engine.MessageLoop.QuerySent += (o, e) => count++;
            engine.Add (new ReadOnlyMemory<byte>[] { new byte[100] });
            Assert.AreEqual (0, engine.MessageLoop.PendingQueries);
            Assert.AreEqual (0, count);
            Assert.AreEqual (0, engine.RoutingTable.CountNodes ());
            Assert.AreEqual (0, engine.MessageLoop.DhtMessageFactory.RegisteredMessages);
        }
    }
}
