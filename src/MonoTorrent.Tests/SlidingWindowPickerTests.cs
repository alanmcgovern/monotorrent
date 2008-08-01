using System;
using System.Collections.Generic;
using System.Text;

using MonoTorrent.Client;
using MonoTorrent.Client.Messages.Standard;

using MonoTorrentTests;

using NUnit.Framework;

namespace MonoTorrent.Tests
{
    [TestFixture]
    public class SlidingWindowPickerTests : PiecePickerTests
    {
        private SlidingWindowPicker swp;

        [SetUp]
        public void Setup()
        {
            // Yes, this is horrible. Deal with it.
            rig = new TestRig("");
            peers = new List<PeerId>();
            picker = new SlidingWindowPicker(10);
            swp = picker as SlidingWindowPicker;
            picker.Initialise(rig.Manager.Bitfield, rig.Manager.Torrent.Files, new List<Piece>(), new MonoTorrent.Common.BitField(rig.Manager.Bitfield.Length));
            peer = new PeerId(new Peer(new string('a', 20), new Uri("tcp://BLAH")), rig.Manager);
            for (int i = 0 ; i < 20 ; i++)
            {
                PeerId p = new PeerId(new Peer(new string(i.ToString()[0], 20), new Uri("tcp://" + i)), rig.Manager);
                p.SupportsFastPeer = true;
                peers.Add(p);
            }
        }

        [Test]
        public void SetSizes()
        {
            for (int high = 1 ; high < 10 ; high++)
            {
                swp.HighPrioritySetSize = high;
                Assert.AreEqual(high, swp.HighPrioritySetSize);
                
                for (int ratio = 1 ; ratio < 10 ; ratio++)
                {
                    swp.MediumToHighRatio = ratio;

                    Assert.AreEqual(ratio, swp.MediumToHighRatio);
                    Assert.AreEqual(high, swp.HighPrioritySetSize);
                    Assert.AreEqual(high * ratio, swp.MediumPrioritySetSize);
                }
            }
        }

        [Test]
        public void PickPiece()
        {
            swp.HighPrioritySetSize = 4;
            swp.MediumToHighRatio = 4;

            Assert.IsTrue(swp.HighPrioritySetSize * swp.MediumToHighRatio < this.rig.Torrent.Pieces.Count);
            Console.WriteLine(rig.Torrent.Pieces.Count);

            peer.BitField.SetAll(true);
            peer.IsChoking = false;
            foreach (PeerId p in peers)
            {
                p.IsChoking = false;
            }

            int nextIndex = 0;
            int blocksRequested = 0;
            while (!rig.Manager.Bitfield.AllTrue)
            {
                RequestMessage msg = swp.PickPiece(peer, peers);

                Console.WriteLine(msg);
                Assert.IsNotNull(msg);
                Assert.IsTrue(msg.PieceIndex == nextIndex,
                    String.Format("Next index: {0}, Piece Index: {1}", nextIndex, msg.PieceIndex));

                Piece req = null;
                foreach (Piece piece in swp.Requests)
                {
                    if (piece.Index == msg.PieceIndex)
                        req = piece;
                }
                Assert.IsNotNull(req);
                    
                if(++blocksRequested == req.BlockCount)     // all blocks requested
                {
                    // mark as received
                    rig.Manager.Bitfield[nextIndex++] = true;
                    swp.HighPrioritySetSize++;
                    blocksRequested = 0;
                }
            }
        }
    }
}
