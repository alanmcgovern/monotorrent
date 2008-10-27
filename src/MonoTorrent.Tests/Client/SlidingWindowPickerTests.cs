using System;
using System.Collections.Generic;
using System.Text;

using MonoTorrent.Client;
using MonoTorrent.Client.Messages.Standard;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class SlidingWindowPickerTests : PiecePickerTests
    {
        private SlidingWindowPicker swp;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            
            picker = new SlidingWindowPicker(10);
            swp = picker as SlidingWindowPicker;
            picker.Initialise(rig.Manager.Bitfield, rig.Manager.Torrent.Files, new List<Piece>(), new MonoTorrent.Common.BitField(rig.Manager.Bitfield.Length));
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


        /// <summary>
        /// Slide the high priority set start along the torrent and make sure pieces are downloaded in order
        /// </summary>
        [Test]
        public void HighPriorityOnly()
        {
            swp.HighPrioritySetSize = 4;
            swp.MediumToHighRatio = 4;

            Assert.IsTrue(swp.HighPrioritySetSize * swp.MediumToHighRatio < this.rig.Torrent.Pieces.Count);
            Console.WriteLine(rig.Torrent.Pieces.Count);

            peer.BitField.SetAll(true);
            peer.IsChoking = false;
            foreach (PeerId p in peers)
            {
                p.BitField.SetAll(true);
                p.IsChoking = false;
            }

            int curPiece = 0;
            int blocksRequested = 0;
            while (!rig.Manager.Bitfield.AllTrue)
            {
                RequestMessage msg = swp.PickPiece(peer, peers);

                Assert.IsNotNull(msg);
                Assert.IsTrue(msg.PieceIndex == curPiece,
                    String.Format("Next index: {0}, Piece Index: {1}", curPiece, msg.PieceIndex));

                Piece req = GetPieceRequest(msg.PieceIndex);
                Assert.IsNotNull(req);

                if (++blocksRequested == req.BlockCount)     // all blocks requested
                {
                    // mark as received
                    rig.Manager.Bitfield[curPiece++] = true;
                    swp.HighPrioritySetSize++;
                    blocksRequested = 0;
                }
            }
        }


        /// <summary>
        /// Make sure high priority is requested first, then medium priority, then low
        /// </summary>
        [Test]
        public void PriorityOrder()
        {
            swp.HighPrioritySetSize = 4;
            swp.MediumToHighRatio = 4;

            Assert.IsTrue(swp.HighPrioritySetSize * swp.MediumToHighRatio < this.rig.Torrent.Pieces.Count);
            Console.WriteLine(rig.Torrent.Pieces.Count);

            peer.BitField.SetAll(true);
            peer.IsChoking = false;
            foreach (PeerId p in peers)
            {
                p.BitField.SetAll(true);
                p.IsChoking = false;
            }

            int curPiece = 0;
            int blocksRequested = 0;
            while (curPiece < swp.HighPrioritySetSize)
            {
                RequestMessage msg = swp.PickPiece(peer, peers);

                Assert.IsNotNull(msg);
                Assert.IsTrue(msg.PieceIndex == curPiece,
                    String.Format("Next index: {0}, Piece Index: {1}", curPiece, msg.PieceIndex));

                Piece req = GetPieceRequest(msg.PieceIndex);
                Assert.IsNotNull(req);

                if (++blocksRequested == req.BlockCount)     // all blocks requested
                {
                    // mark as received
                    rig.Manager.Bitfield[curPiece++] = true;
                    blocksRequested = 0;
                }
            }

            curPiece = -1;
            while (!rig.Manager.Bitfield.AllTrue)
            {
                RequestMessage msg = swp.PickPiece(peer, peers);

                Assert.IsNotNull(msg);
                Assert.IsTrue(msg.PieceIndex == curPiece || curPiece == -1,
                    String.Format("Next index: {0}, Piece Index: {1}", curPiece, msg.PieceIndex));

                if (msg.PieceIndex > swp.HighPrioritySetStart + swp.HighPrioritySetSize + swp.MediumPrioritySetSize)
                {
                    for (int i = 0 ; i < swp.MediumPrioritySetSize ; i++)
                    {
                        int index = swp.HighPrioritySetStart + swp.HighPrioritySetSize + i;
                        Assert.IsTrue(index >= rig.Manager.Bitfield.Length || rig.Manager.Bitfield[i]);
                    }
                }

                if (curPiece == -1)
                    curPiece = msg.PieceIndex;

                Piece req = GetPieceRequest(msg.PieceIndex);
                Assert.IsNotNull(req);

                if (++blocksRequested == req.BlockCount)     // all blocks requested
                {
                    // mark as received
                    rig.Manager.Bitfield[curPiece] = true;
                    curPiece = -1;
                    blocksRequested = 0;
                }
            }
        }


        /// <summary>
        /// Put every piece on a different peer and make sure PriorityOrder still works
        /// </summary>
        [Test]
        public void EfficientRequest()
        {
            peer.BitField.SetAll(false);
            peer.IsChoking = false;
            foreach (PeerId p in peers)
            {
                p.BitField.SetAll(false);
                p.IsChoking = false;
            }

            List<PeerId> allPeers = new List<PeerId>(peers);
            allPeers.Add(peer);

            for (int i = 0 ; i < rig.Manager.Bitfield.Length ; i++)
            {
                allPeers[i % allPeers.Count].BitField[i] = true;
            }

            Dictionary<int, int> blocksRequested = new Dictionary<int, int>();
            while (!rig.Manager.Bitfield.AllTrue)
            {
                RequestMessage msg = null;
                foreach (PeerId p in allPeers)
                {
                    List<PeerId> otherPeers = new List<PeerId>(allPeers);
                    otherPeers.Remove(p);
                    msg = swp.PickPiece(p, otherPeers);

                    if (msg != null)
                        break;
                }
                Assert.IsNotNull(msg);

                if (!blocksRequested.ContainsKey(msg.PieceIndex))
                    blocksRequested.Add(msg.PieceIndex, 1);
                else
                    blocksRequested[msg.PieceIndex]++;

                Piece req = GetPieceRequest(msg.PieceIndex);
                Assert.IsNotNull(req);

                if (blocksRequested[req.Index] == req.BlockCount)     // all blocks requested
                {
                    // mark as received
                    rig.Manager.Bitfield[req.Index] = true;
                }
            }
        }


        private Piece GetPieceRequest(int index)
        {
            Piece req = null;
            foreach (Piece piece in swp.Requests)
            {
                if (piece.Index == index)
                    req = piece;
            }
            return req;
        }

        public override void CancelRequests()
        {
            // This test won't work for the sliding window picker
        }
    }
}
