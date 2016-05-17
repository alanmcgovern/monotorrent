using System;
using System.Collections.Generic;
using System.Text;

using MonoTorrent.Client;
using MonoTorrent.Client.Messages.Standard;

using Xunit;

namespace MonoTorrent.Client
{
    //
    //public class SlidingWindowPickerTests : PiecePickerTests
    //{
    //    private SlidingWindowPicker swp;

    //    [SetUp]
    //    public override void Setup()
    //    {
    //        base.Setup();
            
    //        picker = new SlidingWindowPicker(10);
    //        swp = picker as SlidingWindowPicker;
    //        picker.Initialise(rig.Manager.Bitfield, rig.Manager.Torrent.Files, new List<Piece>(), new MonoTorrent.Common.BitField(rig.Manager.Bitfield.Length));
    //    }


    //    [Fact]
    //    public void SetSizes()
    //    {
    //        for (int high = 1 ; high < 10 ; high++)
    //        {
    //            swp.HighPrioritySetSize = high;
    //            Assert.Equal(high, swp.HighPrioritySetSize);

    //            for (int ratio = 1 ; ratio < 10 ; ratio++)
    //            {
    //                swp.MediumToHighRatio = ratio;

    //                Assert.Equal(ratio, swp.MediumToHighRatio);
    //                Assert.Equal(high, swp.HighPrioritySetSize);
    //                Assert.Equal(high * ratio, swp.MediumPrioritySetSize);
    //            }
    //        }
    //    }


    //    /// <summary>
    //    /// Slide the high priority set start along the torrent and make sure pieces are downloaded in order
    //    /// </summary>
    //    [Fact]
    //    public void HighPriorityOnly()
    //    {
    //        swp.HighPrioritySetSize = 4;
    //        swp.MediumToHighRatio = 4;

    //        Assert.True(swp.HighPrioritySetSize * swp.MediumToHighRatio < this.rig.Torrent.Pieces.Count);
    //        Console.WriteLine(rig.Torrent.Pieces.Count);

    //        peer.BitField.SetAll(true);
    //        peer.IsChoking = false;
    //        foreach (PeerId p in peers)
    //        {
    //            p.BitField.SetAll(true);
    //            p.IsChoking = false;
    //        }

    //        int curPiece = 0;
    //        int blocksRequested = 0;
    //        while (!rig.Manager.Bitfield.AllTrue)
    //        {
    //            RequestMessage msg = swp.PickPiece(peer, peers);

    //            Assert.NotNull(msg);
    //            Assert.True(msg.PieceIndex == curPiece,
    //                String.Format("Next index: {0}, Piece Index: {1}", curPiece, msg.PieceIndex));

    //            Piece req = GetPieceRequest(msg.PieceIndex);
    //            Assert.NotNull(req);

    //            if (++blocksRequested == req.BlockCount)     // all blocks requested
    //            {
    //                // mark as received
    //                rig.Manager.Bitfield[curPiece++] = true;
    //                swp.HighPrioritySetSize++;
    //                blocksRequested = 0;
    //            }
    //        }
    //    }


    //    /// <summary>
    //    /// Make sure high priority is requested first, then medium priority, then low
    //    /// </summary>
    //    [Fact]
    //    public void PriorityOrder()
    //    {
    //        swp.HighPrioritySetSize = 4;
    //        swp.MediumToHighRatio = 4;

    //        Assert.True(swp.HighPrioritySetSize * swp.MediumToHighRatio < this.rig.Torrent.Pieces.Count);
    //        Console.WriteLine(rig.Torrent.Pieces.Count);

    //        peer.BitField.SetAll(true);
    //        peer.IsChoking = false;
    //        foreach (PeerId p in peers)
    //        {
    //            p.BitField.SetAll(true);
    //            p.IsChoking = false;
    //        }

    //        int curPiece = 0;
    //        int blocksRequested = 0;
    //        while (curPiece < swp.HighPrioritySetSize)
    //        {
    //            RequestMessage msg = swp.PickPiece(peer, peers);

    //            Assert.NotNull(msg);
    //            Assert.True(msg.PieceIndex == curPiece,
    //                String.Format("Next index: {0}, Piece Index: {1}", curPiece, msg.PieceIndex));

    //            Piece req = GetPieceRequest(msg.PieceIndex);
    //            Assert.NotNull(req);

    //            if (++blocksRequested == req.BlockCount)     // all blocks requested
    //            {
    //                // mark as received
    //                rig.Manager.Bitfield[curPiece++] = true;
    //                blocksRequested = 0;
    //            }
    //        }

    //        curPiece = -1;
    //        while (!rig.Manager.Bitfield.AllTrue)
    //        {
    //            RequestMessage msg = swp.PickPiece(peer, peers);

    //            Assert.NotNull(msg);
    //            Assert.True(msg.PieceIndex == curPiece || curPiece == -1,
    //                String.Format("Next index: {0}, Piece Index: {1}", curPiece, msg.PieceIndex));

    //            if (msg.PieceIndex > swp.HighPrioritySetStart + swp.HighPrioritySetSize + swp.MediumPrioritySetSize)
    //            {
    //                for (int i = 0 ; i < swp.MediumPrioritySetSize ; i++)
    //                {
    //                    int index = swp.HighPrioritySetStart + swp.HighPrioritySetSize + i;
    //                    Assert.True(index >= rig.Manager.Bitfield.Length || rig.Manager.Bitfield[i]);
    //                }
    //            }

    //            if (curPiece == -1)
    //                curPiece = msg.PieceIndex;

    //            Piece req = GetPieceRequest(msg.PieceIndex);
    //            Assert.NotNull(req);

    //            if (++blocksRequested == req.BlockCount)     // all blocks requested
    //            {
    //                // mark as received
    //                rig.Manager.Bitfield[curPiece] = true;
    //                curPiece = -1;
    //                blocksRequested = 0;
    //            }
    //        }
    //    }


    //    /// <summary>
    //    /// Put every piece on a different peer and make sure PriorityOrder still works
    //    /// </summary>
    //    [Fact]
    //    public void EfficientRequest()
    //    {
    //        peer.BitField.SetAll(false);
    //        peer.IsChoking = false;
    //        foreach (PeerId p in peers)
    //        {
    //            p.BitField.SetAll(false);
    //            p.IsChoking = false;
    //        }

    //        List<PeerId> allPeers = new List<PeerId>(peers);
    //        allPeers.Add(peer);

    //        for (int i = 0 ; i < rig.Manager.Bitfield.Length ; i++)
    //        {
    //            allPeers[i % allPeers.Count].BitField[i] = true;
    //        }

    //        Dictionary<int, int> blocksRequested = new Dictionary<int, int>();
    //        while (!rig.Manager.Bitfield.AllTrue)
    //        {
    //            RequestMessage msg = null;
    //            foreach (PeerId p in allPeers)
    //            {
    //                List<PeerId> otherPeers = new List<PeerId>(allPeers);
    //                otherPeers.Remove(p);
    //                msg = swp.PickPiece(p, otherPeers);

    //                if (msg != null)
    //                    break;
    //            }
    //            Assert.NotNull(msg);

    //            if (!blocksRequested.ContainsKey(msg.PieceIndex))
    //                blocksRequested.Add(msg.PieceIndex, 1);
    //            else
    //                blocksRequested[msg.PieceIndex]++;

    //            Piece req = GetPieceRequest(msg.PieceIndex);
    //            Assert.NotNull(req);

    //            if (blocksRequested[req.Index] == req.BlockCount)     // all blocks requested
    //            {
    //                // mark as received
    //                rig.Manager.Bitfield[req.Index] = true;
    //            }
    //        }
    //    }


    //    private Piece GetPieceRequest(int index)
    //    {
    //        Piece req = null;
    //        foreach (Piece piece in swp.Requests)
    //        {
    //            if (piece.Index == index)
    //                req = piece;
    //        }
    //        return req;
    //    }

    //    public override void CancelRequests()
    //    {
    //        // This test won't work for the sliding window picker
    //    }
    //}
}
