using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client.PiecePicking
{
    public class IgnoringPicker : PiecePicker
    {
        BitField bitfield;
        BitField temp;

        public IgnoringPicker(BitField bitfield, PiecePicker picker)
            : base(picker)
        {
            this.bitfield = bitfield;
            this.temp = new BitField(bitfield.Length);
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int startIndex, int endIndex, int count)
        {
            // Invert 'bitfield' and AND it with the peers bitfield
            // Any pieces which are 'true' in the bitfield will not be downloaded
            temp.SetAll(false).Or(peerBitfield).NAnd(bitfield);
            return base.PickPiece(id, temp, otherPeers, startIndex, endIndex, count);
        }

        public override bool IsInteresting(BitField bitfield)
        {
            temp.SetAll(false).Or(bitfield).NAnd(this.bitfield);
            return base.IsInteresting(temp);
        }
    }
}
