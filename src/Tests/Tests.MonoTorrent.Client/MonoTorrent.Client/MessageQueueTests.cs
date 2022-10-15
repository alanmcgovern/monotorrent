using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.Messages.Peer;
using MonoTorrent.Messages.Peer.FastPeer;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class MessageQueueTests
    {
        [Test]
        public void RejectRequestDisposesMessage ()
        {
            var queue = new MessageQueue ();
            queue.SetReady ();

            var bufferReleaser = MemoryPool.Default.Rent (Constants.BlockSize, out Memory<byte> buffer);
            (var msg, var msgReleaser) = PeerMessage.Rent<PieceMessage> ();
            msg.Initialize (0, 0, Constants.BlockSize);

            msg.SetData ((bufferReleaser, buffer));
            queue.Enqueue (msg, msgReleaser);

            Assert.IsFalse (msg.Data.IsEmpty);
            queue.RejectRequests (false, new int[0]);
            Assert.IsTrue (msg.Data.IsEmpty);

            Assert.IsNull (queue.TryDequeue ());
        }

        [Test]
        public void RejectFastMessage ()
        {
            var queue = new MessageQueue ();
            queue.SetReady ();

            var bufferReleaser = MemoryPool.Default.Rent (Constants.BlockSize, out Memory<byte> buffer);
            (var msg, var msgReleaser) = PeerMessage.Rent<PieceMessage> ();
            msg.Initialize (0, 0, Constants.BlockSize);

            msg.SetData ((bufferReleaser, buffer));
            queue.Enqueue (msg, msgReleaser);

            Assert.IsFalse (msg.Data.IsEmpty);
            queue.RejectRequests (true, new int[] { 0 });
            Assert.IsFalse (msg.Data.IsEmpty);

            Assert.AreSame (msg, queue.TryDequeue ());
        }

        [Test]
        public void RejectNonFastMessage ()
        {
            var queue = new MessageQueue ();
            queue.SetReady ();

            var bufferReleaser = MemoryPool.Default.Rent (Constants.BlockSize, out Memory<byte> buffer);
            (var msg, var msgReleaser) = PeerMessage.Rent<PieceMessage> ();
            msg.Initialize (0, 0, Constants.BlockSize);

            msg.SetData ((bufferReleaser, buffer));
            queue.Enqueue (msg, msgReleaser);

            Assert.IsFalse (msg.Data.IsEmpty);
            queue.RejectRequests (true, new int[] { 1 });
            Assert.IsTrue (msg.Data.IsEmpty);

            Assert.IsInstanceOf<RejectRequestMessage>(queue.TryDequeue ());
        }

        [Test]
        public void CancelRequestDisposesMessage ()
        {
            var queue = new MessageQueue ();
            queue.SetReady ();

            var bufferReleaser = MemoryPool.Default.Rent (Constants.BlockSize, out Memory<byte> buffer);
            (var msg, var msgReleaser) = PeerMessage.Rent<PieceMessage> ();
            msg.Initialize(0, 0, Constants.BlockSize);

            msg.SetData ((bufferReleaser, buffer));
            queue.Enqueue (msg, msgReleaser);

            Assert.IsFalse (msg.Data.IsEmpty);
            Assert.IsTrue (queue.TryCancelRequest (0, 0, Constants.BlockSize));
            Assert.IsTrue (msg.Data.IsEmpty);

            Assert.IsNull (queue.TryDequeue ());
        }
    }
}
