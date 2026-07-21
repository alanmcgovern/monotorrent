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
        public async Task BootstrapRouters ()
        {
            var engine = new DhtEngine ();
            Assert.IsNotEmpty (engine.BootstrapRouters);

            await engine.SetBootstrapRoutersAsync (Array.Empty<BootstrapRouter> ());

            Assert.IsEmpty (engine.BootstrapRouters);
            await engine.SetBootstrapRoutersAsync (new[] {
                new BootstrapRouter ("test", 123),
                new BootstrapRouter ("test", 123),
                new BootstrapRouter ("test", 123)
            });
            Assert.AreEqual (1, engine.BootstrapRouters.Count);
        }

        [Test]
        public async Task AddRawNodesBeforeStarting ()
        {
            int count = 0;
            var engine = new DhtEngine ();
            engine.MessageLoop.QuerySent += (o, e) => count++;
            await engine.AddAsync (new ReadOnlyMemory<byte>[] { new byte[100] });
            Assert.AreEqual (0, engine.MessageLoop.PendingQueries);
            Assert.AreEqual (0, count);
            Assert.AreEqual (0, engine.RoutingTable.CountNodes ());
            Assert.AreEqual (0, engine.MessageLoop.DhtMessageFactory.RegisteredMessages);
        }
    }
}
