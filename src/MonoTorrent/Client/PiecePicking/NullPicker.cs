using System.Collections.Generic;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class NullPicker : PiecePicker
    {
        public NullPicker()
            : base(null)
        {
        }

        public override void CancelRequest(PeerId peer, int piece, int startOffset, int length)
        {
        }

        public override void CancelRequests(PeerId peer)
        {
        }

        public override void CancelTimedOutRequests()
        {
        }

        public override int CurrentRequestCount()
        {
            return 0;
        }

        public override List<Piece> ExportActiveRequests()
        {
            return new List<Piece>();
        }

        public override void Initialise(BitField bitfield, TorrentFile[] files,
            IEnumerable<Piece> requests)
        {
        }

        public override bool IsInteresting(BitField bitfield)
        {
            return false;
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int count,
            int startIndex, int endIndex)
        {
            return null;
        }

        public override void Reset()
        {
        }

        public override bool ValidatePiece(PeerId peer, int pieceIndex, int startOffset, int length, out Piece piece)
        {
            piece = null;
            return false;
        }
    }
}