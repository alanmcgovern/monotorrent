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
        private object requestsLocker = new object();
        private List<Piece> pieces;
        private List<Block> blocks;
        Dictionary<PeerConnectionID, List<Block>> requests;
        #endregion


        #region Constructors

        public EndGamePicker(BitField myBitfield, Torrent torrent, Dictionary<PeerConnectionID, List<Piece>> existingRequests)
        {
            this.myBitfield = myBitfield;
            this.requests = new Dictionary<PeerConnectionID, List<Block>>();
            this.pieces = new List<Piece>();
            for (int i = 0; i < this.myBitfield.Length; i++)
                if (!this.myBitfield[i])
                    this.pieces.Add(new Piece(i, torrent));

            AddExistingRequests(existingRequests);

            this.blocks = new List<Block>(this.pieces.Count * this.pieces[0].Blocks.Length);
            for (int i = 0; i < this.pieces.Count; i++)
                for (int j = 0; j < this.pieces[i].Blocks.Length; j++)
                    this.blocks.Add(this.pieces[i][j]);
        }

        private void AddExistingRequests(Dictionary<PeerConnectionID, List<Piece>> existingRequests)
        {
            foreach (KeyValuePair<PeerConnectionID, List<Piece>> keypair in existingRequests)
            {
                if (!this.requests.ContainsKey(keypair.Key))
                    this.requests.Add(keypair.Key, new List<Block>());

                List<Block> requests = this.requests[keypair.Key];
                foreach (Piece p in keypair.Value)
                {
                    int index = this.pieces.IndexOf(p);
                    if (index == -1)
                        this.pieces.Add(p);
                    else
                        this.pieces[index] = p;

                    foreach (Block b in p)
                        if (b.Requested && !b.Received)
                            requests.Add(b);
                }
            }
        }

        #endregion


        #region Methods

        public List<Piece> CurrentPieces()
        {
            return this.pieces;
        }


        public override int CurrentRequestCount()
        {
            int count = 0;
            lock (this.requestsLocker)
                for (int i = 0; i < this.blocks.Count; i++)
                    if (this.blocks[i].Requested && !this.blocks[i].Received)
                        count++;
            return count;
        }


        public override bool IsInteresting(PeerConnectionID id)
        {
            lock (this.requestsLocker)
            {
                for (int i = 0; i < this.pieces.Count; i++)
                    if (id.Peer.Connection.BitField[pieces[i].Index])
                        return true;

                return false;
            }
        }


        public override RequestMessage PickPiece(PeerConnectionID id, List<PeerConnectionID> otherPeers)
        {
            lock (this.requestsLocker)
            {
                for (int i = 0; i < this.blocks.Count; i++)
                {
                    if (!id.Peer.Connection.BitField[this.blocks[i].PieceIndex])
                        continue;

                    Block b = this.blocks[i];
                    this.blocks.RemoveAt(i);
                    this.blocks.Add(b);
                    b.Requested = true;

                    if (!this.requests.ContainsKey(id))
                        this.requests.Add(id, new List<Block>());

                    List<Block> blocks = this.requests[id];
                    blocks.Add(b);

                    return b.CreateRequest();
                }

                return null;
            }
        }


        public override void ReceivedChokeMessage(PeerConnectionID id)
        {
            lock (this.requestsLocker)
            {
                if (!(id.Peer.Connection.SupportsFastPeer && ClientEngine.SupportsFastPeer))
                {
                    if (!this.requests.ContainsKey(id))
                        return;

                    List<Block> blocks = this.requests[id];
                    for (int i = 0; i < blocks.Count; i++)
                        if (!blocks[i].Received && blocks[i].Requested)
                        {
                            id.Peer.Connection.AmRequestingPiecesCount--;
                            blocks[i].Requested = false;
                        }
                }
                else
                {
                    throw new NotImplementedException();
                }
                // Need to sort out who's been requested what and cancel requests off that person
            }
            return;
        }


        public override PieceEvent ReceivedPieceMessage(PeerConnectionID id, byte[] buffer, PieceMessage message)
        {
            lock (this.requestsLocker)
            {
                Piece p = null;
                Block b = null;

                // First get the piece from the list
                for (int i = 0; i < this.pieces.Count; i++)
                    if (this.pieces[i].Index == message.PieceIndex)
                        p = this.pieces[i];

                // If we dont find the piece in our list of requests, we got an unsolicted piece, so we dump it
                if (p == null)
                    return PieceEvent.BlockNotRequested;


                // We then find the corresponding block from that piece
                for (int i = 0; i < p.Blocks.Length; i++)
                    if (p[i].StartOffset == message.StartOffset)
                        b = p[i];


                // If the block doesn't exist, we dump the recieved data
                if (b == null)
                    return PieceEvent.BlockNotRequested;

                if (message.BlockLength != b.RequestLength)
                    throw new Exception("Request length should match block length");

                // Only write to disk once
                if (!b.Received)
                {
                    long writeIndex = (long)message.PieceIndex * message.PieceLength + message.StartOffset;
                    id.TorrentManager.FileManager.Write(buffer, message.DataOffset, writeIndex, message.BlockLength);
                }
                b.Received = true;
                id.Peer.Connection.AmRequestingPiecesCount--;

                if (!p.AllBlocksReceived)
                    return PieceEvent.BlockWrittenToDisk;

                bool result = ToolBox.ByteMatch(id.TorrentManager.Torrent.Pieces[p.Index], id.TorrentManager.FileManager.GetHash(p.Index));
                this.myBitfield[message.PieceIndex] = result;

                id.TorrentManager.HashedPiece(new PieceHashedEventArgs(p.Index, result));

                if (result)
                {
                    for (int i = 0; i < p.Blocks.Length; i++)
                        this.blocks.Remove(p[i]);
                    this.pieces.Remove(p);

                    id.TorrentManager.finishedPieces.Enqueue(p.Index);
                }
                else
                {
                    for (int i = 0; i < p.Blocks.Length; i++)
                    {
                        p.Blocks[i].Requested = false;
                        p.Blocks[i].Received = false;
                    }
                }

                return result ? PieceEvent.HashPassed : PieceEvent.HashFailed;
            }
        }


        public override void ReceivedRejectRequest(PeerConnectionID id, RejectRequestMessage message)
        {
            id.Peer.Connection.AmRequestingPiecesCount--;
            // FIXME: Remove a request
        }


        public override void RemoveRequests(PeerConnectionID id)
        {
            // In End Game mode requests aren't tracked... they should be though
        }

        #endregion
    }
}
