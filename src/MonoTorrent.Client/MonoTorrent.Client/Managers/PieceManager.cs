//
// PieceManager.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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


using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using MonoTorrent.Messages.Peer;
using MonoTorrent.PiecePicking;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Contains the logic for choosing what piece to download next
    /// </summary>
    public class PieceManager
    {
        // For every 10 kB/sec upload a peer has, we request one extra piece above the standard amount
        internal const int BonusRequestPerKb = 10;

        // Default to a minimum of 8 blocks
        internal const int NormalRequestAmount = 8;

        bool Initialised { get; set; }
        TorrentManager Manager { get; }
        IPieceRequester Requester { get; set; }
        BitField PendingHashCheckPieces { get; set; }

        /// <summary>
        /// Returns true when every block has been requested at least once.
        /// </summary>
        internal bool InEndgameMode { get; private set; }

        internal PieceManager (TorrentManager manager)
        {
            Manager = manager;
            PendingHashCheckPieces = new BitField (1);
            Requester = manager.Engine!.Factories.CreatePieceRequester ();
        }

        internal bool PieceDataReceived (PeerId id, PieceMessage message, out bool pieceComplete, HashSet<IRequester> peersInvolved)
        {
            //FIXME: Ensure piece length is correct?
            var isValidLength = Manager.Torrent!.BytesPerBlock (message.PieceIndex, message.StartOffset / Constants.BlockSize) == message.RequestLength;
            if (Initialised && isValidLength && Requester.ValidatePiece (id, new PieceSegment (message.PieceIndex, message.StartOffset / Constants.BlockSize), out pieceComplete, peersInvolved)) {
                // If the piece validated correctly we should indicate that this peer is healthy and is providing the data
                // we requested
                if (id.AmRequestingPiecesCount == 0) {
                    id.LastBlockReceived.Reset ();
                } else {
                    id.LastBlockReceived.Restart ();
                }
                if (pieceComplete)
                    PendingHashCheckPieces[message.PieceIndex] = true;
                return true;
            } else {
                pieceComplete = false;
                return false;
            }
        }

        internal bool IsInteresting (PeerId id)
        {
            // If i have completed the torrent, then no-one is interesting
            if (!Initialised || Manager.Complete)
                return false;

            // FIXME: Move this elsewhere?
            id.Peer.IsSeeder = id.BitField.AllTrue;

            // If the peer is a seeder it may still be un-interesting if some files are marked as 'DoNotDownload'
            return Requester.IsInteresting (id, id.BitField);
        }

        ReadOnlyBitField[] otherAvailableCache = Array.Empty<ReadOnlyBitField> ();
        internal void AddPieceRequests (PeerId id)
        {
            if (Initialised) {
                if (otherAvailableCache.Length < Manager.Peers.ConnectedPeers.Count)
                    otherAvailableCache = new ReadOnlyBitField[Manager.Peers.ConnectedPeers.Count];
                var otherAvailable = otherAvailableCache.AsSpan (0, Manager.Peers.ConnectedPeers.Count);
                otherAvailableCache.AsSpan (Manager.Peers.ConnectedPeers.Count).Clear ();

                for (int i = 0; i < otherAvailable.Length; i++)
                    otherAvailable[i] = Manager.Peers.ConnectedPeers[i].BitField;

                Requester.AddRequests (id, id.BitField, otherAvailable);
            }
        }

        (IRequester, ReadOnlyBitField)[] peersCache = Array.Empty<(IRequester, ReadOnlyBitField )> ();

        internal void AddPieceRequests (List<PeerId> peers)
        {
            if (Initialised) {
                if (peersCache.Length < peers.Count)
                    peersCache = new (IRequester, ReadOnlyBitField)[peers.Count];
                var span = peersCache.AsSpan (0, peers.Count);
                peersCache.AsSpan (peers.Count).Clear ();

                for (int i = 0; i < peers.Count; i++)
                    span[i] = (peers[i], peers[i].BitField);
                Requester.AddRequests (span);
            }
        }

        internal void ChangePicker (IPieceRequester requester)
        {
            if (Manager.State != TorrentState.Stopped)
                throw new InvalidOperationException ($"The {nameof (IPieceRequester)} must be set while the TorrentManager is in the Stopped state.");
            Requester = requester;
            Initialised = false;
        }

        internal void Initialise ()
        {
            if (Manager.HasMetadata) {
                Initialised = true;
                PendingHashCheckPieces = new BitField (Manager.Bitfield.Length);

                var ignorableBitfieds = new[] {
                    Manager.Bitfield,
                    Manager.PendingV2PieceHashes,
                    PendingHashCheckPieces,
                    Manager.UnhashedPieces,
                };
                Requester.Initialise (Manager, Manager, ignorableBitfieds);
            }
        }

        public async Task<int> CurrentRequestCountAsync ()
        {
            if (!Initialised)
                return 0;

            await ClientEngine.MainLoop;
            return Requester.CurrentRequestCount ();
        }

        internal void PieceHashed (int pieceIndex)
        {
            if (Initialised)
                PendingHashCheckPieces[pieceIndex] = false;
        }

        internal void CancelRequests (PeerId id)
        {
            if (Initialised)
                Requester.CancelRequests (id, 0, Manager.Torrent!.PieceCount () - 1);
        }

        internal void RequestRejected (PeerId id, BlockInfo pieceRequest)
        {
            if (Initialised && Manager.Torrent!.BytesPerBlock (pieceRequest.PieceIndex, pieceRequest.StartOffset / Constants.BlockSize) == pieceRequest.RequestLength)
                Requester.RequestRejected (id, pieceRequest.ToPieceSegment ());
        }
    }
}
