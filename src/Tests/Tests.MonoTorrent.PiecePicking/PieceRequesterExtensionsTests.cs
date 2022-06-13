using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

namespace MonoTorrent.PiecePicking
{
    [TestFixture]
    public class PieceRequesterExtensionsTests
    {
        [Test]
        public void SmallFinalBlock_SinglePieceSingleBlock ()
        {
            var info = TestTorrentManagerInfo.Create (Constants.BlockSize * 2, Constants.BlockSize / 2);
            Span<PieceSegment> segment = stackalloc PieceSegment[1];
            Span<BlockInfo> block = stackalloc BlockInfo[1];
            segment[0] = new PieceSegment (0, 0);
            segment.ToBlockInfo (block, info);

            Assert.AreEqual (0, block[0].PieceIndex);
            Assert.AreEqual (Constants.BlockSize / 2, block[0].RequestLength);
            Assert.AreEqual (0, block[0].StartOffset);
        }

        [Test]
        public void SmallFinalBlock_MultiPieceSingleBlock ()
        {
            var info = TestTorrentManagerInfo.Create (Constants.BlockSize * 2, Constants.BlockSize * 5 - 1);
            Span<PieceSegment> segment = stackalloc PieceSegment[1];
            Span<BlockInfo> block = stackalloc BlockInfo[1];

            // piece 0, block 0
            segment[0] = new PieceSegment (0, 0);
            segment.ToBlockInfo (block, info);
            Assert.AreEqual (0, block[0].PieceIndex);
            Assert.AreEqual (Constants.BlockSize, block[0].RequestLength);
            Assert.AreEqual (0, block[0].StartOffset);

            // piece 0, block 1
            segment[0] = new PieceSegment (0, 1);
            segment.ToBlockInfo (block, info);
            Assert.AreEqual (0, block[0].PieceIndex);
            Assert.AreEqual (Constants.BlockSize, block[0].StartOffset);
            Assert.AreEqual (Constants.BlockSize, block[0].RequestLength);

            // piece 1, block 0
            segment[0] = new PieceSegment (1, 0);
            segment.ToBlockInfo (block, info);
            Assert.AreEqual (1, block[0].PieceIndex);
            Assert.AreEqual (0, block[0].StartOffset);
            Assert.AreEqual (Constants.BlockSize, block[0].RequestLength);

            // piece 1, block 1
            segment[0] = new PieceSegment (1, 1);
            segment.ToBlockInfo (block, info);
            Assert.AreEqual (1, block[0].PieceIndex);
            Assert.AreEqual (Constants.BlockSize, block[0].StartOffset);
            Assert.AreEqual (Constants.BlockSize, block[0].RequestLength);

            // piece 2, block 0
            segment[0] = new PieceSegment (2, 0);
            segment.ToBlockInfo (block, info);
            Assert.AreEqual (2, block[0].PieceIndex);
            Assert.AreEqual (0, block[0].StartOffset);
            Assert.AreEqual (Constants.BlockSize - 1, block[0].RequestLength);
        }

        [Test]
        public void SmallFinalBlock_MultiPieceMultiBlock ()
        {
            var info = TestTorrentManagerInfo.Create (Constants.BlockSize * 2, Constants.BlockSize * 5 + 1);
            Span<PieceSegment> segment = stackalloc PieceSegment[1];
            Span<BlockInfo> block = stackalloc BlockInfo[1];

            // piece 2, block 1
            segment[0] = new PieceSegment (2, 1);
            segment.ToBlockInfo (block, info);
            Assert.AreEqual (2, block[0].PieceIndex);
            Assert.AreEqual (Constants.BlockSize, block[0].StartOffset);
            Assert.AreEqual (1, block[0].RequestLength);
        }
    }
}
