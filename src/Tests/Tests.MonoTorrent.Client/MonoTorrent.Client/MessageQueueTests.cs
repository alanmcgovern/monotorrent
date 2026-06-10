using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.Messages.Peer;
using MonoTorrent.Messages;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class MessageQueueTests
    {
        [Test]
        public void RejectFastMessage ()
        {
            var queue = new MessageQueue ();
            queue.SetReady ();

            (var msg, var msgReleaser) = MessageEncoder.WriteSparsePiece (0, 0, Constants.BlockSize);

            queue.Enqueue (msg, msgReleaser);
            queue.RejectRequests (true, new int[] { 0 });
            Assert.IsTrue (msg.Span.SequenceEqual (queue.TryDequeue ().Span));
        }

        [Test]
        public void RejectNonFastMessage ()
        {
            var queue = new MessageQueue ();
            queue.SetReady ();

            (var msg, var msgReleaser) = MessageEncoder.WriteSparsePiece (0, 0, Constants.BlockSize);
            queue.Enqueue (msg, msgReleaser);

            queue.RejectRequests (true, new int[] { 1 });
            var popped = queue.TryDequeue ();
            Assert.IsFalse (msg.Span.SequenceEqual (popped.Span));

            Assert.AreEqual (MessageType.RejectRequest, MessageDispatcher.GetType (popped));
        }
    }
}
