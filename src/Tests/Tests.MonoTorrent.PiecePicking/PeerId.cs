using System;
using System.Collections.Generic;

namespace MonoTorrent.PiecePicking
{
    class PeerId : IRequester
    {
        internal static PeerId CreateNull (int bitfieldLength)
        {
            return CreateNull (bitfieldLength, false, true, false);
        }

        /// <summary>
        /// Creates a PeerID with a null TorrentManager and IConnection. This is used for unit testing purposes.
        /// A bitfield with all pieces set to <see langword="false"/> will be created too.
        /// </summary>
        /// <param name="bitfieldLength"></param>
        /// <param name="seeder">True if the returned peer should be treated as a seeder (the bitfield will have all pieces set to 'true')</param>
        /// <param name="isChoking"></param>
        /// <param name="amInterested"></param>
        /// <returns></returns>
        internal static PeerId CreateNull (int bitfieldLength, bool seeder, bool isChoking, bool amInterested)
        {
            var peer = new PeerId {
                IsChoking = isChoking,
                AmInterested = amInterested,
                BitField = new BitField (bitfieldLength).SetAll (seeder)
            };
            return peer;
        }

        public bool AmInterested { get; set; }
        public int AmRequestingPiecesCount { get; set; }
        public BitField BitField { get; private set; }
        public bool CanRequestMorePieces { get; set; } = true;
        public long DownloadSpeed { get; }
        public List<int> IsAllowedFastPieces { get; } = new List<int> ();
        public bool IsChoking { get; set; } = true;
        public bool IsSeeder { get; }
        public int MaxPendingRequests { get; set; } = 256;
        public int RepeatedHashFails { get; set; }
        public List<int> SuggestedPieces { get; } = new List<int> ();
        public bool SupportsFastPeer { get; set; }
        public int TotalHashFails { get; set; }
        public bool CanCancelRequests { get; }

        public List<BlockInfo> Requests { get; } = new List<BlockInfo> ();
        public void EnqueueCancellation (BlockInfo request)
        {
            throw new NotImplementedException ();
        }

        public void EnqueueCancellations (IList<BlockInfo> requests)
        {
            throw new NotImplementedException ();
        }

        public void EnqueueRequest (BlockInfo request)
        {
            Requests.Add (request);
        }

        public void EnqueueRequests (Span<BlockInfo> requests)
        {
            for (int i = 0; i < requests.Length; i++)
                Requests.Add (requests[i]);
        }

        public int PreferredRequestAmount (int pieceLength)
        {
            return 1;
        }
    }
}
