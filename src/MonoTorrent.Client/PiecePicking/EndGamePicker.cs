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
    internal class EndGamePicker : IPiecePicker
    {
        #region Member Variables
        private object requestsLocker = new object();
        private List<Piece> pieces;
        private List<Block> blocks;
        private List<PeerConnectionID> requesters;
        #endregion


        #region Constructors
        public EndGamePicker(BitField myBitfield, Torrent torrent)
        {
            this.myBitfield = myBitfield;
            this.pieces = new List<Piece>(this.myBitfield.Length - this.myBitfield.TrueCount);
            for (int i = 0; i < this.myBitfield.Length; i++)
                if (!this.myBitfield[i])
                    this.pieces.Add(new Piece(i, torrent));

            this.blocks = new List<Block>(this.pieces.Count * this.pieces[0].Blocks.Length);
            for (int i = 0; i < this.pieces.Count; i++)
                for (int j = 0; j < this.pieces[i].Blocks.Length; j++)
                    this.blocks.Add(this.pieces[i][j]);
        }
        #endregion


        #region Methods
        public bool IsInteresting(PeerConnectionID id)
        {
            lock (this.requestsLocker)
            {
                for(int i=0; i<this.pieces.Count;i++)
                    if (id.Peer.Connection.BitField[pieces[i].Index])
                        return true;

                return false;
            }
        }

        public RequestMessage PickPiece(PeerConnectionID id, Peers otherPeers)
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
                    return b.CreateRequest();
                }

                return null;
            }
        }

        public BitField MyBitField
        {
            get { return this.myBitfield; }
        }
        private BitField myBitfield;


        public List<Piece> CurrentPieces()
        {
            return this.pieces;
        }


        public int CurrentRequestCount()
        {
            return this.pieces.Count;
        }


        public void ReceivedRejectRequest(PeerConnectionID id, RejectRequestMessage message)
        {
            // FIXME: Remove a request
        }


        public void RemoveRequests(PeerConnectionID id)
        {
            // In End Game mode requests aren't tracked... they should be though
        }


        public PieceEvent ReceivedPieceMessage(PeerConnectionID id, byte[] buffer, int dataOffset, long writeIndex, int blockLength, PieceMessage message)
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
                    id.TorrentManager.FileManager.Write(buffer, dataOffset, writeIndex, blockLength);
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

                    id.TorrentManager.PieceCompleted(p.Index);
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
        #endregion


    }
}

#region crap
/*
        List<Piece> endGamePieces;
        private RequestMessage EndGamePicker(PeerConnectionID id, Peers otherPeers)
        {
            lock (this.myBitfield)
            {
                if (this.endGamePieces == null)
                    CalculateEndGamePieces(id);

                for (int i = 0; i < this.endGamePieces.Count; i++)
                {
                    if (!id.Peer.Connection.BitField[this.endGamePieces[i].Index])
                        continue;

                    Piece p = this.endGamePieces[i];
                    for (int j = 0; j < p.Blocks.Length; j++)
                    {
                        if (p[j].Received)
                            continue;

                        p[j].Requested = true;
                        return p[j].CreateRequest();
                    }
                }
            }

            return null;    // He has nothing we want
        }


        private void CalculateEndGamePieces(PeerConnectionID id)
        {
            this.endGameActive = true;
            this.endGamePieces = new List<Piece>();
            for (int i = 0; i < this.myBitfield.Length; i++)
                if (!this.myBitfield[i])
                {
                    this.myBitfield[i] = true;
                    this.endGamePieces.Add(new Piece(i, id.TorrentManager.Torrent));
                }

            foreach (KeyValuePair<PeerConnectionID, List<Piece>> keypair in this.requests)
                foreach (Piece p in keypair.Value)
                    this.endGamePieces.Add(p);
        }


        private void ReceivedEndGameMessage(PeerConnectionID id, byte[] recieveBuffer, int offset, int writeIndex, int p, PieceMessage message)
        {
            Piece piece = null;
            Block receivedBlock = null;

            lock (this.requests)
            {
                for (int i = 0; i < this.endGamePieces.Count; i++)
                {
                    if (this.endGamePieces[i].Index != message.PieceIndex)
                        continue;

                    piece = this.endGamePieces[i];
                    for (int j = 0; j < piece.Blocks.Length; j++)
                        if (piece[j].StartOffset == message.StartOffset)
                            receivedBlock = piece[j];

                    if (receivedBlock == null)
                        return;

                    id.Peer.Connection.AmRequestingPiecesCount--;
                    if (receivedBlock.RequestLength != message.BlockLength)
                        throw new Exception("Request length should match block length");

                    receivedBlock.Received = true;
                    id.TorrentManager.FileManager.Write(recieveBuffer, offset, writeIndex, p);


                    if (!piece.AllBlocksReceived)
                        return;

                    bool result = ToolBox.ByteMatch(id.TorrentManager.Torrent.Pieces[piece.Index], id.TorrentManager.FileManager.GetHash(piece.Index));
                    this.myBitfield[message.PieceIndex] = result;

                    id.TorrentManager.HashedPiece(new PieceHashedEventArgs(piece.Index, result));

                    if (result)
                    {
                        id.Peer.Connection.IsInterestingToMe = this.IsInteresting(id);
                        id.TorrentManager.PieceCompleted(piece.Index);
                        this.endGamePieces.Remove(piece);
                    }
                    else
                    {
                        for (int k = 0; k < piece.Blocks.Length; k++)
                        {
                            piece[k].Requested = false;
                            piece[k].Received = false;
                        }
                    }
                }
            }
        }
*/

#endregion