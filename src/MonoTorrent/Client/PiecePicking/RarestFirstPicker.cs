using System.Collections.Generic;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class RarestFirstPicker : PiecePicker
    {
        private int length;
        private readonly Stack<BitField> rarest;
        private readonly Stack<BitField> spares;

        public RarestFirstPicker(PiecePicker picker)
            : base(picker)
        {
            rarest = new Stack<BitField>();
            spares = new Stack<BitField>();
        }

        private BitField DequeueSpare()
        {
            return spares.Count > 0 ? spares.Pop() : new BitField(length);
        }

        public override void Initialise(BitField bitfield, TorrentFile[] files, IEnumerable<Piece> requests)
        {
            base.Initialise(bitfield, files, requests);
            length = bitfield.Length;
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int count,
            int startIndex, int endIndex)
        {
            if (peerBitfield.AllFalse)
                return null;

            if (count > 1)
                return base.PickPiece(id, peerBitfield, otherPeers, count, startIndex, endIndex);

            GenerateRarestFirst(peerBitfield, otherPeers);

            while (rarest.Count > 0)
            {
                var current = rarest.Pop();
                var bundle = base.PickPiece(id, current, otherPeers, count, startIndex, endIndex);
                spares.Push(current);

                if (bundle != null)
                    return bundle;
            }

            return null;
        }

        private void GenerateRarestFirst(BitField peerBitfield, List<PeerId> otherPeers)
        {
            // Move anything in the rarest buffer into the spares
            while (rarest.Count > 0)
                spares.Push(rarest.Pop());

            var current = DequeueSpare();
            current.From(peerBitfield);

            // Store this bitfield as the first iteration of the Rarest First algorithm.
            rarest.Push(current);

            // Get a cloned copy of the bitfield and begin iterating to find the rarest pieces
            for (var i = 0; i < otherPeers.Count; i++)
            {
                if (otherPeers[i].BitField.AllTrue)
                    continue;

                current = DequeueSpare().From(current);

                // currentBitfield = currentBitfield & (!otherBitfield)
                // This calculation finds the pieces this peer has that other peers *do not* have.
                // i.e. the rarest piece.
                current.NAnd(otherPeers[i].BitField);

                // If the bitfield now has no pieces we've completed our task
                if (current.AllFalse)
                {
                    spares.Push(current);
                    break;
                }

                // Otherwise push the bitfield on the stack and clone it and iterate again.
                rarest.Push(current);
            }
        }
    }
}