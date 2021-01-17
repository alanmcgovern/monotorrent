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
using System.Linq;
using System.Threading.Tasks;

using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.PiecePicking;

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

        TorrentManager Manager { get; }
        IPiecePicker originalPicker;
        internal IPiecePicker Picker { get; private set; }
        internal BitField PendingHashCheckPieces { get; private set; }

        internal PieceManager (TorrentManager manager)
        {
            Manager = manager;
            Picker = new NullPicker ();
            PendingHashCheckPieces = new BitField (1);
        }

        internal bool PieceDataReceived (PeerId id, PieceMessage message, out bool pieceComplete, out IList<IPieceRequester> peersInvolved)
        {
            if (Picker.ValidatePiece (id, new PieceRequest (message.PieceIndex, message.StartOffset, message.RequestLength), out pieceComplete, out peersInvolved)) {
                id.LastBlockReceived.Restart ();
                if (pieceComplete)
                    PendingHashCheckPieces[message.PieceIndex] = true;
                return true;
            } else {
                return false;
            }
        }

        internal void AddPieceRequests (PeerId id)
        {
            int maxRequests = id.MaxPendingRequests;

            if (id.AmRequestingPiecesCount >= maxRequests)
                return;

            int count = 1;
            if (id.Connection is HttpConnection) {
                if (id.AmRequestingPiecesCount > 0)
                    return;

                // How many whole pieces fit into 2MB
                count = (2 * 1024 * 1024) / Manager.Torrent.PieceLength;

                // Make sure we have at least one whole piece
                count = Math.Max (count, 1);

                count *= Manager.Torrent.PieceLength / Piece.BlockSize;
            }

            if (!id.IsChoking || id.SupportsFastPeer) {
                while (id.AmRequestingPiecesCount < maxRequests) {
                    PieceRequest? request = Picker.ContinueExistingRequest (id, 0, id.BitField.Length - 1);
                    if (request != null)
                        id.MessageQueue.Enqueue (new RequestMessage (request.Value.PieceIndex, request.Value.StartOffset, request.Value.RequestLength));
                    else
                        break;
                }
            }

            if (!id.IsChoking || (id.SupportsFastPeer && id.IsAllowedFastPieces.Count > 0)) {
                while (id.AmRequestingPiecesCount < maxRequests) {
                    List<PeerId> otherPeers = Manager.Peers.ConnectedPeers ?? new List<PeerId> ();
                    IList<PieceRequest> request = Picker.PickPiece (id, id.BitField, otherPeers, count, 0, Manager.Bitfield.Length - 1);
                    if (request != null && request.Count > 0)
                        id.MessageQueue.Enqueue (new RequestBundle (request));
                    else
                        break;
                }
            }

            if (!id.IsChoking && id.AmRequestingPiecesCount == 0) {
                while (id.AmRequestingPiecesCount < maxRequests) {
                    PieceRequest? request = Picker.ContinueAnyExistingRequest (id, 0, Manager.Bitfield.Length - 1);
                    if (request != null)
                        id.MessageQueue.Enqueue (new RequestMessage (request.Value.PieceIndex, request.Value.StartOffset, request.Value.RequestLength));
                    else
                        break;
                }
            }
        }

        internal bool IsInteresting (PeerId id)
        {
            // If i have completed the torrent, then no-one is interesting
            if (Manager.Complete)
                return false;

            // If the peer is a seeder, then he is definately interesting
            if ((id.Peer.IsSeeder = id.BitField.AllTrue))
                return true;

            // Otherwise we need to do a full check
            return Picker.IsInteresting (id, id.BitField);
        }

        internal void ChangePicker (IPiecePicker picker, BitField bitfield)
        {
            originalPicker = picker;
            if (PendingHashCheckPieces.Length != bitfield.Length)
                PendingHashCheckPieces = new BitField (bitfield.Length);

            // 'PendingHashCheckPieces' is the list of fully downloaded pieces which
            // are waiting to be hash checked. We should not begin a second download of
            // a piece while waiting to confirm if the original download was successful.
            //
            // 'Manager.UnhashedPieces' represents the pieces from the torrent which
            // have not been hash checked as they are marked as 'DoNotDownload'. If
            // a file is changed to be downloadable, the engine will hashcheck the data
            // first and then remove them from the 'UnhashedPieces' bitfield which will
            // make them downloadable. If they actually passed the hashcheck then they
            // won't actually be requested again.
            picker = new IgnoringPicker (bitfield, picker);
            picker = new IgnoringPicker (PendingHashCheckPieces, picker);
            picker = new IgnoringPicker (Manager.UnhashedPieces, picker);
            Picker = picker;
        }

        internal void RefreshPickerWithMetadata (BitField bitfield, ITorrentData data)
        {
            ChangePicker (originalPicker, bitfield);
            Picker.Initialise (bitfield, data, Enumerable.Empty<ActivePieceRequest> ());
        }

        internal void Reset ()
        {
            PendingHashCheckPieces.SetAll (false);
            Picker?.Initialise (Manager.Bitfield, Manager, Array.Empty<ActivePieceRequest> ());
        }

        public async Task<int> CurrentRequestCountAsync ()
        {
            await ClientEngine.MainLoop;
            return Picker.CurrentRequestCount ();
        }
    }
}
