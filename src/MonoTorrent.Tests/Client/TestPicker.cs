using System.Collections.Generic;
using MonoTorrent.Client;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;

namespace MonoTorrent.Tests.Client
{
    internal class TestPicker : PiecePicker
    {
        public List<BitField> IsInterestingBitfield = new List<BitField>();

        public List<int> PickedPieces = new List<int>();
        public List<BitField> PickPieceBitfield = new List<BitField>();
        public List<int> PickPieceCount = new List<int>();
        public List<int> PickPieceEndIndex = new List<int>();
        public List<PeerId> PickPieceId = new List<PeerId>();
        public List<List<PeerId>> PickPiecePeers = new List<List<PeerId>>();
        public List<int> PickPieceStartIndex = new List<int>();

        public bool ReturnNoPiece = true;

        public TestPicker()
            : base(null)
        {
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int count,
            int startIndex, int endIndex)
        {
            PickPieceId.Add(id);
            var clone = new BitField(peerBitfield.Length);
            clone.Or(peerBitfield);
            PickPieceBitfield.Add(clone);
            PickPiecePeers.Add(otherPeers);
            PickPieceStartIndex.Add(startIndex);
            PickPieceEndIndex.Add(endIndex);
            PickPieceCount.Add(count);

            for (var i = startIndex; i < endIndex; i++)
            {
                if (PickedPieces.Contains(i))
                    continue;
                PickedPieces.Add(i);
                if (ReturnNoPiece)
                    return null;
                return new MessageBundle();
            }
            return null;
        }

        public override void Initialise(BitField bitfield, TorrentFile[] files, IEnumerable<Piece> requests)
        {
        }

        public override bool IsInteresting(BitField bitfield)
        {
            IsInterestingBitfield.Add(bitfield);
            return !bitfield.AllFalse;
        }
    }
}