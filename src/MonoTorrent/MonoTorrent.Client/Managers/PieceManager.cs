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

namespace MonoTorrent.Client
{
    /// <summary>
    /// Contains the logic for choosing what piece to download next
    /// </summary>
    public class PieceManager
    {
        #region Internal Constants

        // For every 10 kB/sec upload a peer has, we request one extra piece above the standard amount him
        internal const int BonusRequestPerKb = 10;  
        internal const int NormalRequestAmount = 2;
        internal const int MaxEndGameRequests = 2;

        #endregion


        #region Events

        public event EventHandler<BlockEventArgs> BlockReceived;
        public event EventHandler<BlockEventArgs> BlockRequested;
        public event EventHandler<BlockEventArgs> BlockRequestCancelled;

        #endregion


        #region Member Variables

        private PiecePickerBase piecePicker;

        #endregion


        #region Properties

        /// <summary>
        /// This option changes the picking algorithm from rarest first to linear. This should
        /// only be enabled if the content being downloaded is streaming audio/video. It degrades
        /// overall performance of the swarm.
        /// </summary>
        public bool LinearPickingEnabled
        {
            get { return piecePicker.LinearPickingEnabled; }
            set { piecePicker.LinearPickingEnabled = value; }
        }


        /// <summary>
        /// Get the PiecePicker instance that is currently being used by the PieceManager
        /// </summary>
        public PiecePickerBase PiecePicker
        {
            get { return this.piecePicker; }
        }


        #endregion Properties


        #region Constructors

        internal PieceManager(BitField bitfield, TorrentFile[] files)
        {
            piecePicker = new StandardPicker();
            piecePicker.Initialise(bitfield, files, new List<Piece>(), new BitField(bitfield.Length));
        }

        #endregion


        #region Methods

        /// <summary>
        /// Tries to add a piece request to the peers message queue.
        /// </summary>
        /// <param name="id">The peer to add the request too</param>
        /// <returns>True if the request was added</returns>
        internal bool AddPieceRequest(PeerId id)
        {
            PeerMessage msg;

            // If someone can upload to us fast, queue more pieces off them. But no more than 100 blocks.
            int maxRequests = PieceManager.NormalRequestAmount + (int)(id.Monitor.DownloadSpeed / 1024.0 / BonusRequestPerKb);
			maxRequests = maxRequests > 50 ? 50 : maxRequests;

            if (id.AmRequestingPiecesCount >= maxRequests)
                return false;

            if (this.InEndGameMode)// In endgame we only want to queue 2 pieces
                if (id.AmRequestingPiecesCount > PieceManager.MaxEndGameRequests)
                    return false;

            msg = this.PickPiece(id, id.TorrentManager.Peers.ConnectedPeers);
            if (msg == null)
                return false;

            id.Enqueue(msg);
            id.AmRequestingPiecesCount++;
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
            return this.piecePicker.IsInteresting(id);
        }


        public bool InEndGameMode
        {
            get { return false; }
        }


        internal BitField MyBitField
        {
            get { return this.piecePicker.MyBitField; }
        }


        internal int CurrentRequestCount()
        {
            return this.piecePicker.CurrentRequestCount();
        }


        internal PeerMessage PickPiece(PeerId id, List<PeerId> otherPeers)
        {
            //if ((this.MyBitField.Length - this.MyBitField.TrueCount < 15) && this.piecePicker is StandardPicker)
            //    this.piecePicker = new EndGamePicker(this.MyBitField, id.TorrentManager.Torrent, ((StandardPicker)this.piecePicker).Requests);

            return this.piecePicker.PickPiece(id, otherPeers);
        }

        internal MessageBundle PickPiece(PeerId id, List<PeerId> otherPeers, int count)
        {
            return this.piecePicker.PickPiece(id, otherPeers, count);
        }


        internal void ReceivedChokeMessage(PeerId id)
        {
            this.piecePicker.ReceivedChokeMessage(id);
        }


        internal void ReceivedRejectRequest(PeerId id, RejectRequestMessage msg)
        {
            this.piecePicker.ReceivedRejectRequest(id, msg);
        }


        internal void RemoveRequests(PeerId id)
        {
            this.piecePicker.RemoveRequests(id);
        }

        internal PieceEvent ReceivedPieceMessage(BufferedIO data)
        {
            return this.piecePicker.ReceivedPieceMessage(data);
        }

        internal void Reset()
        {
            this.piecePicker.Reset();
        }

        internal BitField UnhashedPieces
        {
            get { return ((StandardPicker)this.piecePicker).UnhashedPieces; }
        }

        #endregion


        #region Event Firing Code

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

        #endregion Event Firing Code

        internal void ChangePicker(PiecePickerBase picker, TorrentFile[] files)
        {
            picker.Initialise(piecePicker.MyBitField, files, piecePicker.ExportActiveRequests(), piecePicker.UnhashedPieces);
            piecePicker = picker;
        }
    }
}
