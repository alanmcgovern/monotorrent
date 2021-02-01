using System;
using System.Collections.Generic;
using System.Text;

using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client.PiecePicking
{
    class RequestManager : IRequestManager
    {
        BitField Bitfield { get; set; }
        ITorrentData TorrentData { get; set; }

        public bool InEndgameMode { get; private set; }
        public IPiecePicker Picker { get; }

        public RequestManager (IPiecePicker picker)
        {
            Picker = picker;
        }

        public void Initialise (BitField bitfield, ITorrentData torrentData, IEnumerable<ActivePieceRequest> requests)
        {
            Bitfield = bitfield;
            TorrentData = torrentData;
            Picker.Initialise (bitfield, torrentData, requests);
        }

        public void AddRequests (IReadOnlyList<IPeerWithMessaging> peers)
        {
            foreach (var peer in peers)
                AddRequests (peer, peers);
        }

        public void AddRequests (IPeerWithMessaging peer, IReadOnlyList<IPeerWithMessaging> allPeers)
        {
            int maxRequests = peer.MaxPendingRequests;

            if (!peer.CanRequestMorePieces)
                return;

            int count = peer.PreferredRequestAmount (TorrentData.PieceLength);

            if (!peer.IsChoking || peer.SupportsFastPeer) {
                while (peer.AmRequestingPiecesCount < maxRequests) {
                    PieceRequest? request = Picker.ContinueExistingRequest (peer, 0, peer.BitField.Length - 1);
                    if (request != null)
                        peer.EnqueueRequest (request.Value);
                    else
                        break;
                }
            }

            if (!peer.IsChoking || (peer.SupportsFastPeer && peer.IsAllowedFastPieces.Count > 0)) {
                while (peer.AmRequestingPiecesCount < maxRequests) {
                    IList<PieceRequest> request = Picker.PickPiece (peer, peer.BitField, allPeers, count, 0, Bitfield.Length - 1);
                    if (request != null && request.Count > 0)
                        peer.EnqueueRequests (request);
                    else
                        break;
                }
            }

            if (!peer.IsChoking && peer.AmRequestingPiecesCount == 0) {
                while (peer.AmRequestingPiecesCount < maxRequests) {
                    PieceRequest? request = Picker.ContinueAnyExistingRequest (peer, 0, TorrentData.PieceCount () - 1, 1);
                    // If this peer is a seeder and we are unable to request any new blocks, then we should enter
                    // endgame mode. Every block has been requested at least once at this point.
                    if (request == null && (InEndgameMode || peer.IsSeeder)) {
                        request = Picker.ContinueAnyExistingRequest (peer, 0, TorrentData.PieceCount () - 1, 2);
                        // FIXME: What if the picker is choosing to not allocate pieces? Then it's not endgame mode.
                        // This should be deterministic, not a heuristic?
                        InEndgameMode |= request != null && (Bitfield.Length - Bitfield.TrueCount) < 10;
                    }

                    if (request != null)
                        peer.EnqueueRequest (request.Value);
                    else
                        break;
                }
            }
        }
    }
}
