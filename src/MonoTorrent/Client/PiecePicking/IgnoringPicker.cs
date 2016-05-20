using System.Collections.Generic;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class IgnoringPicker : PiecePicker
    {
        private readonly BitField bitfield;
        private readonly BitField temp;

        public IgnoringPicker(BitField bitfield, PiecePicker picker)
            : base(picker)
        {
            this.bitfield = bitfield;
            temp = new BitField(bitfield.Length);
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int count,
            int startIndex, int endIndex)
        {
            // Invert 'bitfield' and AND it with the peers bitfield
            // Any pieces which are 'true' in the bitfield will not be downloaded
            temp.From(peerBitfield).NAnd(bitfield);
            if (temp.AllFalse)
                return null;
            return base.PickPiece(id, temp, otherPeers, count, startIndex, endIndex);
        }

        public override bool IsInteresting(BitField bitfield)
        {
            temp.From(bitfield).NAnd(this.bitfield);
            if (temp.AllFalse)
                return false;
            return base.IsInteresting(temp);
        }
    }
}