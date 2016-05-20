using System;
using System.Collections.Generic;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class RandomisedPicker : PiecePicker
    {
        private readonly Random random = new Random();

        public RandomisedPicker(PiecePicker picker)
            : base(picker)
        {
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int count,
            int startIndex, int endIndex)
        {
            if (peerBitfield.AllFalse)
                return null;

            if (count > 1)
                return base.PickPiece(id, peerBitfield, otherPeers, count, startIndex, endIndex);

            var midpoint = random.Next(startIndex, endIndex);
            return base.PickPiece(id, peerBitfield, otherPeers, count, midpoint, endIndex) ??
                   base.PickPiece(id, peerBitfield, otherPeers, count, startIndex, midpoint);
        }
    }
}