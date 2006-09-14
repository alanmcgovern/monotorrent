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
using MonoTorrent.Client.PeerMessages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Contains the logic for choosing what piece to download next
    /// </summary>
    public class PieceManager : IPieceManager
    {
        #region Events
        /// <summary>
        /// Event that's fired every time a Piece changes
        /// </summary>
        public event EventHandler<PieceEventArgs> OnPieceChanged;
        #endregion


        #region Member Variables
        private Dictionary<PeerConnectionID, Piece> requests;
        private TorrentFile[] torrentFiles;

        /// <summary>
        /// The bitfield for the torrent
        /// </summary>
        public BitField MyBitField
        {
            get { return this.mybitField; }
        }
        private BitField mybitField;

        public int[] priorities;
        #endregion


        #region Constructors
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bitField"></param>
        public PieceManager(BitField bitField, TorrentFile[] torrentFiles)
        {
            this.torrentFiles = torrentFiles;
            this.mybitField = bitField;
            this.requests = new Dictionary<PeerConnectionID, Piece>(32);
            this.isInterestingBuffer = new BitField(bitField.Length);
            this.bufferField = new BitField(bitField.Length);

            this.priorities = (int[])Enum.GetValues(typeof(Priority));
            Array.Sort<int>(this.priorities);
            Array.Reverse(this.priorities);
        }
        #endregion


        #region Methods
        BitField bufferField;
        /// <summary>
        /// Requests the next available piece from the supplied peer
        /// </summary>
        /// <param name="id">The peer to request a piece from</param>
        /// <returns>A RequestMessage for the next free piece</returns>
        public RequestMessage RequestPiece(PeerConnectionID id)
        {
            lock (this.bufferField)
            {
                Piece piece;
                lock (this.mybitField)
                    Array.Copy(this.mybitField.Array, 0, bufferField.Array, 0, this.mybitField.Array.Length);

                bufferField.Not();
                bufferField.And(id.Peer.Connection.BitField);

                if (requests.ContainsKey(id))       // There's a pending request on this peer
                {
                    piece = requests[id];
                    foreach (Block block in piece)
                    {
                        if (!block.Requested)
                        {
                            block.Requested = true;
                            return block.CreateRequest();
                        }
                    }

                    return null;        // We've requested all the blocks in this piece
                }

                int i = -1;
                for (int k = 0; k < (this.priorities.Length - 1); k++)    // ignore "DoNotDownloads"
                {
                    for (int j = 0; j < this.torrentFiles.Length; j++)
                    {
                        if (this.torrentFiles[j].Priority != (Priority)k)
                            continue;

                        i = bufferField.FirstTrue(this.torrentFiles[j].StartPieceIndex, this.torrentFiles[j].EndPieceIndex);
                        if (i == -1)
                            continue;

                        break;
                    }
                }

                if (i == -1)
                    return null;

                piece = new Piece(i, id.TorrentManager.Torrent);
                piece[0].Requested = true;
                this.mybitField[i] = true;  // Flag this one as downloaded. If it hashfails, remove it
                this.requests.Add(id, piece);
                return piece[0].CreateRequest();
            }
        }

        /// <summary>
        /// Returns the number of outstanding requests
        /// </summary>
        public int CurrentRequestCount
        {
            get
            {
                lock (this.requests)
                    return this.requests.Count;
            }
        }

        private BitField isInterestingBuffer;
        /// <summary>
        /// Checks to see if a peer has a piece we want
        /// </summary>
        /// <param name="id">The peer to check to see if he's interesting or not</param>
        /// <returns>True if the peer is interesting, false otherwise</returns>
        public bool IsInteresting(PeerConnectionID id)
        {
            lock (this.isInterestingBuffer)     // I reuse a BitField as a buffer so i don't have to keep allocating new ones
            {
                lock(this.mybitField)
                    Array.Copy(mybitField.Array, 0, isInterestingBuffer.Array, 0, mybitField.Array.Length);
                
                isInterestingBuffer.Not();
                isInterestingBuffer.And(id.Peer.Connection.BitField);
                if (!isInterestingBuffer.AllFalse())
                    return true;                            // He's interesting if he has a piece we want

                lock (this.requests)
                    return (this.requests.ContainsKey(id)); // OR if we're already requesting a piece off him.
            }
        }

        /// <summary>
        /// Removes any outstanding requests from the supplied peer
        /// </summary>
        /// <param name="id">The peer to remove outstanding requests from</param>
        internal void RemoveRequests(PeerConnectionID id)
        {
            lock (this.requests)
            {
                if (this.requests.ContainsKey(id))
                {
                    Piece piece = this.requests[id];
                    this.mybitField[piece.Index] = false;
                    this.requests.Remove(id);
                }
                id.Peer.Connection.AmRequestingPiecesCount = 0;
            }
        }


#warning Fix this up a little, it's a bit messy after the refactor.
        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="recieveBuffer"></param>
        /// <param name="offset"></param>
        /// <param name="writeIndex"></param>
        /// <param name="p"></param>
        internal void RecievedPieceMessage(PeerConnectionID id, byte[] recieveBuffer, int offset, int writeIndex, int p, PieceMessage message)
        {
            id.TorrentManager.DiskManager.Write(recieveBuffer, offset, writeIndex, p);

            if (this.OnPieceChanged != null)
                this.OnPieceChanged((ITorrentManager)id.TorrentManager, new PieceEventArgs(p, PieceEvent.BlockWrittenToDisk));


            lock (this.requests)
            {
                Piece piece = this.requests[id];
                foreach (Block block in piece)
                {
                    if (block.StartOffset == message.StartOffset)
                    {
                        if (message.BlockLength != block.RequestLength)
                            throw new Exception("Request length should match block length");

                        block.Recieved = true;

                        if (this.OnPieceChanged != null)
                            this.OnPieceChanged((ITorrentManager)id.TorrentManager, new PieceEventArgs(message.PieceIndex, PieceEvent.BlockRecieved));

                        id.Peer.Connection.AmRequestingPiecesCount--;
                        break;
                    }
                }

                if (!piece.AllBlocksRecieved)
                    return;

                if (this.OnPieceChanged != null)
                    this.OnPieceChanged((ITorrentManager)id.TorrentManager, new PieceEventArgs(piece.Index, PieceEvent.PieceRecieved));

                bool result = ToolBox.ByteMatch(id.TorrentManager.Torrent.Pieces[piece.Index], id.TorrentManager.DiskManager.GetHash(piece.Index));
                this.mybitField[message.PieceIndex] = result;

                id.TorrentManager.HashedPiece(new PieceHashedEventArgs(piece.Index, result));

                if (result)
                    id.TorrentManager.PieceRecieved(piece.Index);

                this.requests.Remove(id);
            }
        }
        #endregion

        internal void ReceivedRejectRequest(PeerConnectionID id, RejectRequestMessage rejectRequestMessage)
        {
            lock (this.requests)
            {
                if (!this.requests.ContainsKey(id))
                    return;

                Piece p = this.requests[id];

                if (p.Index != rejectRequestMessage.PieceIndex)
                    return;

                foreach (Block block in p)
                {
                    if (block.StartOffset != rejectRequestMessage.StartOffset)
                        continue;

                    block.Requested = false;
                    break;
                }
            }
        }
    }
}
