//
// StandardPieceRequester.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2021 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System.Collections.Generic;

namespace MonoTorrent.PiecePicking
{
    public class StandardPieceRequester : IPieceRequester
    {
        IReadOnlyList<BitField> IgnorableBitfields { get; set; }
        MutableBitField Temp { get; set; }
        ITorrentData TorrentData { get; set; }

        public bool InEndgameMode { get; private set; }
        public IPiecePicker Picker { get; private set; }

        public void Initialise (ITorrentData torrentData, IReadOnlyList<BitField> ignoringBitfields)
        {
            IgnorableBitfields = ignoringBitfields;
            TorrentData = torrentData;

            Temp = new MutableBitField (TorrentData.PieceCount ());

            IPiecePicker picker = new StandardPicker ();
            picker = new RandomisedPicker (picker);
            picker = new RarestFirstPicker (picker);
            Picker = new PriorityPicker (picker);

            Picker.Initialise (torrentData);
        }

        BitField ApplyIgnorables (BitField primary)
        {
            Temp.From (primary);
            for (int i = 0; i < IgnorableBitfields.Count; i++)
                Temp.NAnd (IgnorableBitfields[i]);
            return Temp;
        }

        public void AddRequests (IReadOnlyList<IPeerWithMessaging> peers)
        {
            for (int i = 0; i < peers.Count; i++) {
                var peer = peers[i];
                if (peer.SuggestedPieces.Count > 0 || (!peer.IsChoking && peer.AmRequestingPiecesCount == 0))
                    AddRequests (peer, peers);
            }
        }

        public void AddRequests (IPeerWithMessaging peer, IReadOnlyList<IPeerWithMessaging> allPeers)
        {
            int maxRequests = peer.MaxPendingRequests;

            if (!peer.CanRequestMorePieces)
                return;

            int count = peer.PreferredRequestAmount (TorrentData.PieceLength);

            // This is safe to invoke. 'ContinueExistingRequest' strongly guarantees that a peer will only
            // continue a piece they have initiated. If they're choking then the only piece they can continue
            // will be a fast piece (if one exists!)
            if (!peer.IsChoking || peer.SupportsFastPeer) {
                while (peer.AmRequestingPiecesCount < maxRequests) {
                    BlockInfo? request = Picker.ContinueExistingRequest (peer, 0, peer.BitField.Length - 1);
                    if (request != null)
                        peer.EnqueueRequest (request.Value);
                    else
                        break;
                }
            }

            // FIXME: Would it be easier if RequestManager called PickPiece(AllowedFastPieces[0]) or something along those lines?
            if (!peer.IsChoking || (peer.SupportsFastPeer && peer.IsAllowedFastPieces.Count > 0)) {
                BitField filtered = null;
                while (peer.AmRequestingPiecesCount < maxRequests) {
                    filtered ??= ApplyIgnorables (peer.BitField);
                    IList<BlockInfo> request = Picker.PickPiece (peer, filtered, allPeers, count, 0, TorrentData.PieceCount () - 1);
                    if (request != null && request.Count > 0)
                        peer.EnqueueRequests (request);
                    else
                        break;
                }
            }

            if (!peer.IsChoking && peer.AmRequestingPiecesCount == 0) {
                while (peer.AmRequestingPiecesCount < maxRequests) {
                    BlockInfo? request = Picker.ContinueAnyExistingRequest (peer, 0, TorrentData.PieceCount () - 1, 1);
                    // If this peer is a seeder and we are unable to request any new blocks, then we should enter
                    // endgame mode. Every block has been requested at least once at this point.
                    if (request == null && (InEndgameMode || peer.IsSeeder)) {
                        request = Picker.ContinueAnyExistingRequest (peer, 0, TorrentData.PieceCount () - 1, 2);
                        InEndgameMode |= request != null;
                    }

                    if (request != null)
                        peer.EnqueueRequest (request.Value);
                    else
                        break;
                }
            }
        }

        public bool ValidatePiece (IPeer peer, BlockInfo blockInfo, out bool pieceComplete, out IList<IPeer> peersInvolved)
            => Picker.ValidatePiece (peer, blockInfo, out pieceComplete, out peersInvolved);

        public bool IsInteresting (IPeer peer, BitField bitfield)
            => Picker.IsInteresting (peer, bitfield);

        public IList<BlockInfo> CancelRequests (IPeer peer, int startIndex, int endIndex)
            => Picker.CancelRequests (peer, startIndex, endIndex);

        public void RequestRejected (IPeer peer, BlockInfo pieceRequest)
            => Picker.RequestRejected (peer, pieceRequest);

        public int CurrentRequestCount ()
            => Picker.CurrentRequestCount ();
    }
}
