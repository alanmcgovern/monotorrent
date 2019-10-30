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
        #region Old
        // For every 10 kB/sec upload a peer has, we request one extra piece above the standard amount him
        internal const int BonusRequestPerKb = 10;  
        internal const int NormalRequestAmount = 2;
        internal const int MaxEndGameRequests = 2;

        public event EventHandler<BlockEventArgs> BlockReceived;
        public event EventHandler<BlockEventArgs> BlockRequested;
        public event EventHandler<BlockEventArgs> BlockRequestCancelled;

        internal void RaiseBlockReceived(BlockEventArgs args)
        {
            BlockReceived?.InvokeAsync (args.TorrentManager, args);
        }

        internal void RaiseBlockRequested(BlockEventArgs args)
        {
            BlockRequested?.InvokeAsync (args.TorrentManager, args);
        }

        internal void RaiseBlockRequestCancelled(BlockEventArgs args)
        {
            BlockRequestCancelled?.InvokeAsync (args.TorrentManager, args);
        }

        #endregion Old

        TorrentManager Manager { get; }
        internal PiecePicker Picker { get; private set; }
        internal BitField UnhashedPieces { get; private set; }

        internal PieceManager(TorrentManager manager)
        {
            Manager = manager;
            Picker = new NullPicker();
            UnhashedPieces = new BitField(0);
        }

        internal Piece PieceDataReceived(PeerId id, PieceMessage message)
        {
            Piece piece;
            if (Picker.ValidatePiece(id, message.PieceIndex, message.StartOffset, message.RequestLength, out piece))
            {
                id.LastBlockReceived.Restart ();
                var block = piece.Blocks [message.StartOffset / Piece.BlockSize];

                if (BlockReceived != null)
                    RaiseBlockReceived(new BlockEventArgs(Manager, block, piece, id));

                if (piece.AllBlocksReceived)
                    UnhashedPieces[message.PieceIndex] = true;
                return piece;
            }
            return null;
        }

        internal void AddPieceRequests(PeerId id)
        {
            int maxRequests = id.MaxPendingRequests;

            if (id.AmRequestingPiecesCount >= maxRequests)
                return;

            int count = 1;
            if (id.Connection is HttpConnection)
            {
                if (id.AmRequestingPiecesCount > 0)
                    return;

                // How many whole pieces fit into 2MB
                count = (2 * 1024 * 1024) / Manager.Torrent.PieceLength;

                // Make sure we have at least one whole piece
                count = Math.Max(count, 1);
                
                count *= Manager.Torrent.PieceLength / Piece.BlockSize;
            }

            if (!id.IsChoking || id.SupportsFastPeer)
            {
                while (id.AmRequestingPiecesCount < maxRequests)
                {
                    var request = Picker.ContinueExistingRequest(id);
                    if (request != null)
                        id.Enqueue(new RequestMessage (request.PieceIndex, request.StartOffset, request.RequestLength));
                    else
                        break;
                } 
            }

            if (!id.IsChoking || (id.SupportsFastPeer && id.IsAllowedFastPieces.Count > 0))
            {
                while (id.AmRequestingPiecesCount < maxRequests)
                {
                    var otherPeers = Manager.Peers.ConnectedPeers ?? new List<PeerId> ();
                    var request = Picker.PickPiece(id, id.BitField, otherPeers, count);
                    if (request != null && request.Count > 0)
                        id.Enqueue(new RequestBundle(request));
                    else
                        break;
                }
            }
        }

        internal bool IsInteresting(PeerId id)
        {
            // If i have completed the torrent, then no-one is interesting
            if (Manager.Complete)
                return false;

            // If the peer is a seeder, then he is definately interesting
            if ((id.Peer.IsSeeder = id.BitField.AllTrue))
                return true;

            // Otherwise we need to do a full check
            return Picker.IsInteresting(id.BitField);
        }

        internal void ChangePicker(PiecePicker picker, BitField bitfield, ITorrentData data)
        {
            if (UnhashedPieces.Length != bitfield.Length)
                UnhashedPieces = new BitField(bitfield.Length);

            picker = new IgnoringPicker(bitfield, picker);
            picker = new IgnoringPicker(UnhashedPieces, picker);
            IEnumerable<Piece> pieces = Picker == null ? new List<Piece>() : Picker.ExportActiveRequests();
            picker.Initialise(bitfield, data, pieces);
            Picker = picker;
        }

        internal void Reset()
        {
            UnhashedPieces.SetAll(false);
            Picker?.Reset ();
        }

        public async Task<int> CurrentRequestCountAsync()
        {
            await ClientEngine.MainLoop;
            return Picker.CurrentRequestCount();
        }
    }
}
