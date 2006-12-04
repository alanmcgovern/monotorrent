//
// StandardPicker.cs
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
using System.Text;
using MonoTorrent.Common;
using MonoTorrent.Client.PeerMessages;

namespace MonoTorrent.Client
{
    internal class StandardPicker : IPiecePicker
    {
        #region Member Variables
        private int[] priorities;

        /// <summary>
        /// The bitfield for the torrent
        /// </summary>
        public BitField MyBitField
        {
            get { return this.myBitfield; }
        }
        private BitField myBitfield;

        private BitField bufferBitfield;
        private BitField previousBitfield;
        private BitField isInterestingBuffer;


        private TorrentFile[] torrentFiles;
        private Dictionary<PeerConnectionID, List<Piece>> requests;


        /// <summary>
        /// Returns the number of outstanding requests
        /// </summary>
        public int CurrentRequestCount()
        {
            int result = 0;
            lock (this.requests)
            {
                foreach (KeyValuePair<PeerConnectionID, List<Piece>> keypair in this.requests)
                    result += keypair.Value.Count;
            }
            return result;
        }
        #endregion


        #region Constructors
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bitField"></param>
        internal StandardPicker(BitField bitField, TorrentFile[] torrentFiles)
        {
            this.torrentFiles = torrentFiles;
            this.requests = new Dictionary<PeerConnectionID, List<Piece>>(32);
            this.isInterestingBuffer = new BitField(bitField.Length);
            this.priorities = (int[])Enum.GetValues(typeof(Priority));
            Array.Sort<int>(this.priorities);
            Array.Reverse(this.priorities);

            this.myBitfield = bitField;
            this.bufferBitfield = new BitField(myBitfield.Length);
            this.previousBitfield = new BitField(myBitfield.Length);
            this.torrentFiles = torrentFiles;
            this.requests = new Dictionary<PeerConnectionID, List<Piece>>();

            // Order the priorities in decending order of priority. i.e. Immediate is first, and DoNotDownload is last
            this.priorities = (int[])Enum.GetValues(typeof(Priority));
            Array.Sort<int>(this.priorities);
            Array.Reverse(this.priorities);
        }
        #endregion


        #region Methods
        public RequestMessage PickPiece(PeerConnectionID id, Peers otherPeers)
        {
            int requestIndex = 0;
            Priority highestPriorityFound = Priority.DoNotDownload;

            lock (this.myBitfield)
            {
                // If there is already a request there, request the next block.
                if (this.requests.ContainsKey(id))
                {
                    List<Piece> reqs = this.requests[id];
                    for (int i = 0; i < reqs.Count; i++)
                        for (int j = 0; j < reqs[i].Blocks.Length; j++)
                            if (!reqs[i][j].Requested)
                            {
                                reqs[i][j].Requested = true;
                                return reqs[i][j].CreateRequest();
                            }
                }

                // We get here if there were no free blocks to request.
                // We see if the peer has suggested any pieces we should request
                while (id.Peer.Connection.SuggestedPieces.Count > 0)
                {
                    // If we already have that piece, then remove it from the suggested pieces
                    if (AlreadyHaveOrRequested(id.Peer.Connection.SuggestedPieces[0]))
                    {
                        id.Peer.Connection.SuggestedPieces.RemoveAt(0);
                        continue;
                    }

                    requestIndex = id.Peer.Connection.SuggestedPieces[0];
                    id.Peer.Connection.SuggestedPieces.RemoveAt(0);
                    return this.GenerateRequest(id, requestIndex);
                }

                // Now we see what pieces the peer has that we don't have and try and request one
                Buffer.BlockCopy(this.myBitfield.Array, 0, bufferBitfield.Array, 0, this.bufferBitfield.Array.Length * 4);
                this.bufferBitfield.Not();
                this.bufferBitfield.And(id.Peer.Connection.BitField);

                // Out of the pieces we have, we look for the highest priority and store it here.
                // We then keep NANDing the bitfield until we no longer have pieces with that priority.
                // Thats when we go back 1 step and download a piece with the original "highestPriorityFound"
                highestPriorityFound = HighestPriorityForAvailablePieces(this.bufferBitfield);

                if (highestPriorityFound == Priority.DoNotDownload)
                    return null;    // Nothing to download here.

                for (int i = 0; i < otherPeers.Count; i++)
                {
                    lock (otherPeers[i])
                    {
                        if (otherPeers[i].Peer.Connection == null)
                            continue;

                        Buffer.BlockCopy(this.bufferBitfield.Array, 0, this.previousBitfield.Array, 0, this.bufferBitfield.Array.Length * 4);
                        this.bufferBitfield.And(otherPeers[i].Peer.Connection.BitField);
                        if (this.bufferBitfield.AllFalse() || highestPriorityFound != HighestPriorityForAvailablePieces(this.bufferBitfield))
                            break;
                    }
                }

                // Once we have a bitfield containing just the pieces we need, we request one.
                // FIXME: we need to make sure we take one from the highest priority available.
                // At the moment it just takes anything.
                requestIndex = 0;
                while (true)
                {
                    requestIndex = this.previousBitfield.FirstTrue(requestIndex, id.Peer.Connection.BitField.Length);

                    if (requestIndex == -1)
                        break;

                    if (AlreadyHaveOrRequested(requestIndex))
                        requestIndex++;
                    else
                        break;
                }

                if (requestIndex < 0)
                    return null;

                return this.GenerateRequest(id, requestIndex);
            }
        }

        private bool AlreadyHaveOrRequested(int index)
        {
            if (this.myBitfield[index])
                return true;

            foreach (KeyValuePair<PeerConnectionID, List<Piece>> keypair in this.requests)
                foreach (Piece p in keypair.Value)
                    if (p.Index == index)
                        return true;

            return false;
        }


        private Priority HighestPriorityForAvailablePieces(BitField bitField)
        {
            for (int i = 0; i < this.priorities.Length - 1; i++)
                for (int j = 0; j < this.torrentFiles.Length; j++)
                    if (this.torrentFiles[j].Priority == (Priority)i)
                        return (Priority)i;

            return Priority.DoNotDownload;
        }


        private RequestMessage GenerateRequest(PeerConnectionID id, int index)
        {
            if (!requests.ContainsKey(id))
                requests.Add(id, new List<Piece>(2));

            List<Piece> reqs = requests[id];

            Piece p = new Piece(index, id.TorrentManager.Torrent);
            reqs.Add(p);
            p[0].Requested = true;
            return p[0].CreateRequest();
        }


        /// <summary>
        /// Checks to see if a peer has a piece we want
        /// </summary>
        /// <param name="id">The peer to check to see if he's interesting or not</param>
        /// <returns>True if the peer is interesting, false otherwise</returns>
        public bool IsInteresting(PeerConnectionID id)
        {
            lock (this.isInterestingBuffer)     // I reuse a BitField as a buffer so i don't have to keep allocating new ones
            {
                lock (this.myBitfield)
                    Array.Copy(this.myBitfield.Array, 0, isInterestingBuffer.Array, 0, this.myBitfield.Array.Length);

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
        public void RemoveRequests(PeerConnectionID id)
        {
            lock (this.requests)
            {
                if (this.requests.ContainsKey(id))
                {
                    List<Piece> pieces = this.requests[id];

                    for (int i = 0; i < pieces.Count; i++)
                        this.myBitfield[pieces[i].Index] = false;

                    this.requests.Remove(id);
                }
                if (id.Peer.Connection != null)
                    id.Peer.Connection.AmRequestingPiecesCount = 0;
            }
        }


        // FIXME: fix this up a little, it's a bit messy after the refactor.
        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="recieveBuffer"></param>
        /// <param name="offset"></param>
        /// <param name="writeIndex"></param>
        /// <param name="p"></param>

        public PieceEvent ReceivedPieceMessage(PeerConnectionID id, byte[] recieveBuffer, int offset, long writeIndex, int p, PieceMessage message)
        {
            lock (this.requests)
            {
                if (!this.requests.ContainsKey(id))
                    return PieceEvent.BlockNotRequested;
#warning I should close the connection, but at the moment i'm in a bug fixing mood

                Piece piece = null;
                List<Piece> pieces = this.requests[id];

                for (int i = 0; i < pieces.Count; i++)
                {
                    if (pieces[i].Index != message.PieceIndex)
                        continue;

                    piece = pieces[i];
                    break;
                }

                if (piece == null)
                {
                    return PieceEvent.BlockNotRequested;
#warning Handle this properly. Does this mean that i should close off the peers connection because they sent me a piece i didn't request?
                }

                foreach (Block block in piece)
                {
                    if (block.StartOffset != message.StartOffset)
                        continue;

                    if (message.BlockLength != block.RequestLength)
                        throw new Exception("Request length should match block length");

                    block.Received = true;


                    id.Peer.Connection.AmRequestingPiecesCount--;
                    break;
                }

                id.TorrentManager.FileManager.Write(recieveBuffer, offset, writeIndex, p);


                if (!piece.AllBlocksReceived)
                    return PieceEvent.BlockWrittenToDisk;

                bool result = ToolBox.ByteMatch(id.TorrentManager.Torrent.Pieces[piece.Index], id.TorrentManager.FileManager.GetHash(piece.Index));
                this.myBitfield[message.PieceIndex] = result;

                id.TorrentManager.HashedPiece(new PieceHashedEventArgs(piece.Index, result));

                if (result)
                    id.TorrentManager.PieceCompleted(piece.Index);
#warning To some tricks if we fail to reduce the amount we have to redownload
                pieces.Remove(piece);

                if (pieces.Count == 0)
                    this.requests.Remove(id);

                return result ? PieceEvent.HashPassed : PieceEvent.HashFailed;
            }
        }


        public void ReceivedRejectRequest(PeerConnectionID id, RejectRequestMessage rejectRequestMessage)
        {
            lock (this.requests)
            {
                if (!this.requests.ContainsKey(id))
                    return;

                Piece p = null;

                foreach (Piece piece in this.requests[id])
                {
                    if (piece.Index != rejectRequestMessage.PieceIndex)
                        continue;
                    p = piece;
                }

                if (p == null)
                {
                    return;
#warning Does this need to close the peers connection too?
                }

                foreach (Block block in p)
                {
                    if (block.StartOffset != rejectRequestMessage.StartOffset)
                        continue;

                    block.Requested = false;
                    break;
                }
            }
        }
        #endregion

        public List<Piece> CurrentPieces()
        {
            List<Piece> pieces = new List<Piece>(this.requests.Count * 2);
            lock (this.requests)
            {
                foreach (KeyValuePair<PeerConnectionID, List<Piece>> keypair in this.requests)
                    for (int i = 0; i < keypair.Value.Count; i++)
                        pieces.Add(keypair.Value[i]);
            }
            return pieces;
        }
    }
}
