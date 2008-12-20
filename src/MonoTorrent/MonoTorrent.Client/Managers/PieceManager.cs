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
using MonoTorrent.Common;
using MonoTorrent.Client;
using System.Threading;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Connections;

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
            Toolbox.RaiseAsyncEvent<BlockEventArgs>(BlockReceived, args.TorrentManager, args);
        }

        internal void RaiseBlockRequested(BlockEventArgs args)
        {
            Toolbox.RaiseAsyncEvent<BlockEventArgs>(BlockRequested, args.TorrentManager, args);
        }

        internal void RaiseBlockRequestCancelled(BlockEventArgs args)
        {
            Toolbox.RaiseAsyncEvent<BlockEventArgs>(BlockRequestCancelled, args.TorrentManager, args);
        }

        #endregion Old

        PiecePicker picker;
        BitField unhashedPieces;

        internal PiecePicker Picker
        {
            get { return picker; }
        }

        internal BitField UnhashedPieces
        {
            get { return unhashedPieces; }
        }

        internal PieceManager(PiecePicker picker, BitField bitfield, TorrentFile[] files)
        {
            unhashedPieces = new BitField(bitfield.Length);
            ChangePicker(picker, bitfield, files);
        }


        public void PieceDataReceived(BufferedIO data)
        {
            Piece piece;
            if (picker.ValidatePiece(data.Id, data.PieceIndex, data.PieceOffset, data.Count, out piece))
            {
                PeerId id = data.Id;
                data.Piece = piece;
                id.AmRequestingPiecesCount--;
                id.LastBlockReceived = DateTime.Now;
                id.TorrentManager.PieceManager.RaiseBlockReceived(new BlockEventArgs(data));
                id.TorrentManager.FileManager.QueueWrite(data);
                
                if (data.Piece.AllBlocksReceived)
                    this.unhashedPieces[data.PieceIndex] = true;
            }
            else
            {
            }
        }




        internal bool AddPieceRequest(PeerId id)
        {
            PeerMessage msg;

            // If someone can upload to us fast, queue more pieces off them. But no more than 100 blocks.
            int maxRequests = PieceManager.NormalRequestAmount + (int)(id.Monitor.DownloadSpeed / 1024.0 / BonusRequestPerKb);
			maxRequests = maxRequests > 50 ? 50 : maxRequests;

            if (id.AmRequestingPiecesCount >= maxRequests)
                return false;

            //if (this.InEndGameMode)// In endgame we only want to queue 2 pieces
            //    if (id.AmRequestingPiecesCount > PieceManager.MaxEndGameRequests)
            //        return false;

            int count = 1;
            if (id.Connection is HttpConnection)
            {
                // How many whole pieces fit into 2MB
                count = (2 * 1024 * 1024) / id.TorrentManager.Torrent.PieceLength;

                // Make sure we have at least one whole piece
                count = Math.Max(count, 1);
                
                count *= id.TorrentManager.Torrent.PieceLength / Piece.BlockSize;
            }

            msg = Picker.PickPiece(id, id.TorrentManager.Peers.ConnectedPeers, count);

            if (msg == null)
                return false;

            id.Enqueue(msg);

            if (msg is RequestMessage)
                id.AmRequestingPiecesCount++;
            else
                id.AmRequestingPiecesCount += ((MessageBundle)msg).Messages.Count;

            return true;
        }

        internal bool IsInteresting(PeerId id)
        {
            // If i have completed the torrent, then no-one is interesting
            if (id.TorrentManager.Complete)
                return false;

            // If the peer is a seeder, then he is definately interesting
            if ((id.Peer.IsSeeder = id.BitField.AllTrue))
                return true;

            // Otherwise we need to do a full check
            return Picker.IsInteresting(id.BitField);
        }

        internal void ChangePicker(PiecePicker picker, BitField bitfield, TorrentFile[] files)
        {
//          MonoTorrent.Client.PiecePicker p = new StandardPicker();
//          p = new RandomisedPicker(p);
//          p = new RarestFirstPicker(p);
//          p = new PriorityPicker(p);
            picker = new IgnoringPicker(bitfield, picker);
            picker = new IgnoringPicker(unhashedPieces, picker);
            IEnumerable<Piece> pieces = Picker == null ? new List<Piece>() : Picker.ExportActiveRequests();
            picker.Initialise(bitfield, files, pieces);
            this.picker = picker;
        }

        internal void Reset()
        {
            this.unhashedPieces.SetAll(false);
        }
    }
}
