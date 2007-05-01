//
// EndGamePicker.cs
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
using MonoTorrent.Client.PeerMessages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal class EndGamePicker : PiecePickerBase
    {
        #region Member Variables
        private object requestsLocker = new object();                   // Used to synchronise access to the lists
        private PieceCollection pieces;                                     // A list of all the remaining pieces to download
        private BlockCollection blocks;                                     // A list of all the blocks in the remaining pieces to download
        Dictionary<PeerId, BlockCollection> requests;             // Used to remember which blocks each peer is downloading
        Dictionary<Block, PeerConnectionIDCollection> blockRequestees;      // Used to remember which peers are getting each block so i can issue cancel messages
        #endregion

        #region Constructors

        public EndGamePicker(BitField myBitfield, Torrent torrent, Dictionary<PeerId, PieceCollection> existingRequests)
        {
            this.myBitfield = myBitfield;
            this.requests = new Dictionary<PeerId, BlockCollection>();
            this.blockRequestees = new Dictionary<Block, PeerConnectionIDCollection>();
            this.pieces = new PieceCollection();

            // For all the pieces that we have *not* requested yet, add them into our list of pieces
            for (int i = 0; i < this.myBitfield.Length; i++)
                if (!this.myBitfield[i])
                    this.pieces.Add(new Piece(i, torrent));

            // Then take the dictionary of existing requests and put them into the list of pieces (overwriting as necessary)
            AddExistingRequests(existingRequests);

            this.blocks = new BlockCollection(this.pieces.Count * this.pieces[0].Blocks.Length);
            for (int i = 0; i < this.pieces.Count; i++)
                for (int j = 0; j < this.pieces[i].Blocks.Length; j++)
                    this.blocks.Add(this.pieces[i].Blocks[j]);
        }


        #endregion


        #region Private Methods

        private void AddExistingRequests(Dictionary<PeerId, PieceCollection> existingRequests)
        {
            foreach (KeyValuePair<PeerId, PieceCollection> keypair in existingRequests)
            {
                if (!this.requests.ContainsKey(keypair.Key))
                    this.requests.Add(keypair.Key, new BlockCollection());

                BlockCollection activeRequests = this.requests[keypair.Key];
                foreach (Piece p in keypair.Value)
                {
                    // If the piece has already been put into the list of pieces, we want to overwrite that
                    // entry with this one. Otherwise we just add this piece in.
                    int index = this.pieces.IndexOf(p);
                    if (index == -1)
                        this.pieces.Add(p);
                    else
                        this.pieces[index] = p;

                    // For each block in that piece that has been requested and not received
                    // we put that block in the peers list of 'requested' blocks.
                    // We also add the peer to the list of people who we are requesting that block off.
                    foreach (Block b in p)
                        if (b.Requested && !b.Received)
                        {
                            activeRequests.Add(b);
                            if (!this.blockRequestees.ContainsKey(b))
                                this.blockRequestees.Add(b, new PeerConnectionIDCollection());

                            this.blockRequestees[b].Add(keypair.Key);
                        }
                }
            }
        }

        #endregion


        #region Public Methods

        public override int CurrentRequestCount()
        {
            return this.blockRequestees.Count;
        }


        public override bool IsInteresting(PeerId id)
        {
            lock (this.requestsLocker)
            {
                // See if the peer has any of the pieces in our list of "To Be Requested" pieces
                for (int i = 0; i < this.pieces.Count; i++)
                    if (id.Peer.Connection.BitField[pieces[i].Index])
                        return true;

                return false;
            }
        }


        public override RequestMessage PickPiece(PeerId id, PeerConnectionIDCollection otherPeers)
        {
            lock (this.requestsLocker)
            {
                // For each block, see if the peer has that piece, and if so, request the block
                for (int i = 0; i < this.blocks.Count; i++)
                {
                    if (!id.Peer.Connection.BitField[this.blocks[i].PieceIndex] || this.blocks[i].Received)
                        continue;

                    Block b = this.blocks[i];
                    this.blocks.RemoveAt(i);
                    b.Requested = true; // "Requested" isn't important for endgame picker. All that matters is if we have the piece or not.
                    this.blocks.Add(b);

                    // Add the block to the list of blocks that we are downloading off this peer
                    if (!this.requests.ContainsKey(id))
                        this.requests.Add(id, new BlockCollection());

                    this.requests[id].Add(b);

                    // Add the peer to the list of people who are downloading this block
                    if (!this.blockRequestees.ContainsKey(b))
                        this.blockRequestees.Add(b, new PeerConnectionIDCollection());

                    this.blockRequestees[b].Add(id);

                    // Return the request for the block
                    id.TorrentManager.PieceManager.RaiseBlockRequested(new BlockEventArgs(b, GetPieceFromIndex(this.pieces, b.PieceIndex), id));
                    return b.CreateRequest();
                }

                return null;
            }
        }


        public override void ReceivedChokeMessage(PeerId id)
        {
            lock (this.requestsLocker)
            {
                if (!(id.Peer.Connection.SupportsFastPeer && ClientEngine.SupportsFastPeer))
                    RemoveRequests(id);
                else
                {
                    // Cleanly remove any pending request messages from the send queue as there's no point in sending them
                    IPeerMessageInternal message;
                    int length = id.Peer.Connection.QueueLength;
                    for (int i = 0; i < length; i++)
                        if ((message = id.Peer.Connection.DeQueue()) is RequestMessage)
                            RemoveRequests(id, (RequestMessage)message);
                        else
                            id.Peer.Connection.EnQueue(message);
                }
            }
            return;
        }

        private void RemoveRequests(PeerId id, RequestMessage requestMessage)
        {
            Piece p = PiecePickerBase.GetPieceFromIndex(this.pieces, requestMessage.PieceIndex);
            int b = PiecePickerBase.GetBlockIndex(p.Blocks, requestMessage.StartOffset, requestMessage.RequestLength);

            this.requests[id].Remove(p.Blocks[b]);
            this.blockRequestees[p.Blocks[b]].Remove(id);
            id.TorrentManager.PieceManager.RaiseBlockRequestCancelled(new BlockEventArgs(p.Blocks[b], p, id));
        }


        public override PieceEvent ReceivedPieceMessage(PeerId id, byte[] buffer, PieceMessage message)
        {
            lock (this.requestsLocker)
            {
                Piece p = PiecePickerBase.GetPieceFromIndex(this.pieces, message.PieceIndex);
                if (p == null)
                    return PieceEvent.BlockNotRequested;

                int blockIndex = PiecePickerBase.GetBlockIndex(p.Blocks, message.StartOffset, message.RequestLength);
                if (blockIndex == -1)
                    return PieceEvent.BlockNotRequested;

                // Only write to disk once
                if (!p.Blocks[blockIndex].Received)
                {
                    long writeIndex = (long)message.PieceIndex * message.PieceLength + message.StartOffset;
                    using (new ReaderLock(id.TorrentManager.FileManager.streamsLock))
                        id.TorrentManager.FileManager.Write(buffer, message.DataOffset, writeIndex, message.RequestLength);
                }

                p.Blocks[blockIndex].Received = true;
                id.Peer.Connection.AmRequestingPiecesCount--;
                id.TorrentManager.PieceManager.RaiseBlockReceived(new BlockEventArgs(p.Blocks[blockIndex], p, id));

                if (!p.AllBlocksReceived)
                    return PieceEvent.BlockWrittenToDisk;

                bool result = id.TorrentManager.Torrent.Pieces.IsValid(id.TorrentManager.FileManager.GetHash(p.Index), p.Index);
                this.myBitfield[message.PieceIndex] = result;

                id.TorrentManager.HashedPiece(new PieceHashedEventArgs(p.Index, result));

                BlockCollection activeRequests = this.requests[id];
                PeerConnectionIDCollection activeRequestees = this.blockRequestees[p.Blocks[blockIndex]];
                activeRequests.Remove(p.Blocks[blockIndex]);
                activeRequestees.Remove(id);

                for (int i = 0; i < activeRequestees.Count; i++)
                    lock (activeRequestees[i])
                        if (activeRequestees[i].Peer.Connection != null)
                            activeRequestees[i].Peer.Connection.EnQueueAt(new CancelMessage(message.PieceIndex, message.StartOffset, message.RequestLength), 0);

                activeRequestees.Clear();
                this.blockRequestees.Remove(p.Blocks[blockIndex]);

                if (result)
                {
                    id.TorrentManager.finishedPieces.Enqueue(p.Index);

                    // Clear the piece and the blocks from the list
                    for (int i = 0; i < p.Blocks.Length; i++)
                        this.blocks.Remove(p.Blocks[i]);
                    this.pieces.Remove(p);
                }
                else
                {
                    for (int i = 0; i < p.Blocks.Length; i++)
                        p.Blocks[i].Received = false;
                }

                return result ? PieceEvent.HashPassed : PieceEvent.HashFailed;
            }
        }


        public override void ReceivedRejectRequest(PeerId id, RejectRequestMessage message)
        {
            lock (this.requestsLocker)
            {
                if (!this.requests.ContainsKey(id))
                    throw new MessageException("Received reject request for a piece i'm not requesting");

                BlockCollection pieces = this.requests[id];

                Piece piece = PiecePickerBase.GetPieceFromIndex(this.pieces, message.PieceIndex);
                int block = PiecePickerBase.GetBlockIndex(piece.Blocks, message.StartOffset, message.RequestLength);

                if (this.requests[id].Contains(piece.Blocks[block]))
                {
                    this.requests[id].Remove(piece.Blocks[block]);
                    this.blockRequestees[piece.Blocks[block]].Remove(id);
                    id.Peer.Connection.AmRequestingPiecesCount--;
                    id.TorrentManager.PieceManager.RaiseBlockRequestCancelled(new BlockEventArgs(piece.Blocks[block], piece, id));
                }
            }
        }

        #endregion

        public override void RemoveRequests(PeerId id)
        {
            if (!this.requests.ContainsKey(id))
                return;

            BlockCollection blocks = this.requests[id];
            for (int i = 0; i < blocks.Count; i++)
            {
                id.Peer.Connection.AmRequestingPiecesCount--;
                id.TorrentManager.PieceManager.RaiseBlockRequestCancelled(new BlockEventArgs(blocks[i], GetPieceFromIndex(this.pieces, blocks[i].PieceIndex), id));

                if (this.blockRequestees.ContainsKey(blocks[i]))
                {
                    PeerConnectionIDCollection requestees = this.blockRequestees[blocks[i]];
                    requestees.Remove(id);
                    if (requestees.Count == 0)
                        this.blockRequestees.Remove(blocks[i]);
                }
            }

            blocks.Clear();
        }
    }
}
