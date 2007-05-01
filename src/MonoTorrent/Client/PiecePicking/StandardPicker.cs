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
    internal class StandardPicker : PiecePickerBase
    {
        #region Member Variables
        private int[] priorities;

        private BitField bufferBitfield;
        private BitField previousBitfield;
        private BitField isInterestingBuffer;


        private TorrentFile[] torrentFiles;
        private IntCollection unhashedPieces;   // Store the index of finished pieces which are not hashed. These count as "AlreadyHaveOrRequested"
        private Dictionary<PeerId, PieceCollection> requests;
        internal Dictionary<PeerId, PieceCollection> Requests
        {
            get { return this.requests; }
        }

        private Random random = new Random();

        /// <summary>
        /// Returns the number of outstanding requests
        /// </summary>
        public override int CurrentRequestCount()
        {
            int result = 0;
            lock (this.requests)
            {
                foreach (KeyValuePair<PeerId, PieceCollection> keypair in this.requests)
                    for (int i = 0; i < keypair.Value.Count; i++)
                        for (int j = 0; j < keypair.Value[i].Blocks.Length; j++)
                            if (keypair.Value[i].Blocks[j].Requested && !keypair.Value[i].Blocks[j].Received)
                                result++;
            }
            return result;
        }
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new piece picker with support for prioritisation of files
        /// </summary>
        /// <param name="bitField">The bitfield associated with the torrent</param>
        /// <param name="torrentFiles">The files that are available in this torrent</param>
        internal StandardPicker(BitField bitField, TorrentFile[] torrentFiles)
        {
            this.torrentFiles = torrentFiles;
            this.requests = new Dictionary<PeerId, PieceCollection>(32);
            this.isInterestingBuffer = new BitField(bitField.Length);
            this.priorities = (int[])Enum.GetValues(typeof(Priority));
            Array.Sort<int>(this.priorities);
            Array.Reverse(this.priorities);

            this.myBitfield = bitField;
            this.bufferBitfield = new BitField(myBitfield.Length);
            this.previousBitfield = new BitField(myBitfield.Length);
            this.torrentFiles = torrentFiles;
            this.unhashedPieces = new IntCollection(8);
            this.requests = new Dictionary<PeerId, PieceCollection>();

            // Order the priorities in decending order of priority. i.e. Immediate is first, and DoNotDownload is last
            this.priorities = (int[])Enum.GetValues(typeof(Priority));
            Array.Sort<int>(this.priorities);
            Array.Reverse(this.priorities);
        }
        #endregion


        #region Methods

        /// <summary>
        /// Helper method that tells us if a specific piece has already been downloaded or is already being requested
        /// </summary>
        /// <param name="index">The index of the piece to check</param>
        /// <returns></returns>
        private bool AlreadyHaveOrRequested(int index)
        {
            if (this.myBitfield[index])
                return true;

            foreach (KeyValuePair<PeerId, PieceCollection> keypair in this.requests)
                for(int i=0; i < keypair.Value.Count; i++)
                    if (keypair.Value[i].Index == index)
                        return true;

            lock (this.unhashedPieces)
                return this.unhashedPieces.Contains(index);
        }


        /// <summary>
        /// When picking a piece, attempt to request the next free block from an existing request if there is one
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private RequestMessage ContinueExistingRequest(PeerId id)
        {
            // Return null if we aren't already tracking pieces for this peer
            if (!this.requests.ContainsKey(id))
                return null;

            // Get the list of all the pieces we're requesting off the peer and check to see if there
            // are any blocks not requested yet. If there are, request them
            PieceCollection reqs = this.requests[id];
            for (int i = 0; i < reqs.Count; i++)
            {
                for (int j = 0; j < reqs[i].Blocks.Length; j++)
                {
                    if (!reqs[i].Blocks[j].Requested)
                    {
                        reqs[i].Blocks[j].Requested = true;
                        return reqs[i].Blocks[j].CreateRequest();
                    }
                }
            }

            // If we get here it means all the blocks in the pieces being downloaded by the peer are already requested
            return null;
        }


        /// <summary>
        /// When a piece is first chosen to be downloaded, the request must be generated
        /// </summary>
        /// <param name="id">The peer to generate the request for</param>
        /// <param name="index">The index of the piece to be requested</param>
        /// <returns></returns>
        private RequestMessage GenerateRequest(PeerId id, int index)
        {
            if (!requests.ContainsKey(id))
                requests.Add(id, new PieceCollection(2));

            PieceCollection reqs = requests[id];

            Piece p = new Piece(index, id.TorrentManager.Torrent);
            reqs.Add(p);
            p.Blocks[0].Requested = true;
            return p.Blocks[0].CreateRequest();
        }


        /// <summary>
        /// When picking a piece, attempt to request a fast piece if there is one available
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private RequestMessage GetFastPiece(PeerId id)
        {
            int requestIndex; 

            // If fast peers isn't supported on both sides, then return null
            if (!(id.Peer.Connection.SupportsFastPeer && ClientEngine.SupportsFastPeer))
                return null;

            while (id.Peer.Connection.IsAllowedFastPieces.Count > 0)
            {
                // If we already have that piece, then remove it from the list so we don't check it again
                if (AlreadyHaveOrRequested((int)id.Peer.Connection.IsAllowedFastPieces[0]))
                {
                    id.Peer.Connection.IsAllowedFastPieces.RemoveAt(0);
                    continue;
                }

                // For all the remaining fast pieces
                for (int i = 0; i < id.Peer.Connection.IsAllowedFastPieces.Count; i++)
                {
                    // If the peer has this piece
                    if (id.Peer.Connection.BitField[(int)id.Peer.Connection.IsAllowedFastPieces[i]])
                    {
                        // We request that piece and remove it from the list
                        requestIndex = (int)id.Peer.Connection.IsAllowedFastPieces[i];
                        id.Peer.Connection.IsAllowedFastPieces.RemoveAt(i);
                        return this.GenerateRequest(id, requestIndex);
                    }
                }

                // If we get here it means that the peer had none of the fast pieces that we're allowed request
                // so it means we can request no fast pieces off them
                break;
            }

            return null;
        }


        /// <summary>
        /// When picking a piece, attempt to request a piece that the peer has recommended that we download
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private RequestMessage GetSuggestedPiece(PeerId id)
        {
            int requestIndex;
            while (id.Peer.Connection.SuggestedPieces.Count > 0)
            {
                // If we already have that piece, then remove it from the suggested pieces
                if (AlreadyHaveOrRequested(id.Peer.Connection.SuggestedPieces[0]))
                {
                    id.Peer.Connection.SuggestedPieces.RemoveAt(0);
                    continue;
                }

                for (int i = 0; i < id.Peer.Connection.SuggestedPieces.Count; i++)
                {
                    if (!id.Peer.Connection.BitField[id.Peer.Connection.SuggestedPieces[i]])
                        continue;

                    requestIndex = id.Peer.Connection.SuggestedPieces[i];
                    id.Peer.Connection.SuggestedPieces.RemoveAt(i);
                    return this.GenerateRequest(id, requestIndex);
                }

                break;
            }

            return null;
        }


        /// <summary>
        /// Checks to see which files have available pieces and returns the highest priority found
        /// </summary>
        /// <param name="bitField"></param>
        /// <returns></returns>
        private Priority HighestPriorityAvailable(BitField bitField)
        {
            Priority highestFound = Priority.DoNotDownload;

            // Find the Highest priority file that is in this torrent
            for (int i = 0; i < this.torrentFiles.Length; i++)
                if ((this.torrentFiles[i].Priority > highestFound) &&
                    (bitField.FirstTrue(this.torrentFiles[i].StartPieceIndex, this.torrentFiles[i].EndPieceIndex) != -1))
                    highestFound = this.torrentFiles[i].Priority;

            return highestFound;
        }


        /// <summary>
        /// Checks to see if a peer has a piece we want
        /// </summary>
        /// <param name="id">The peer to check to see if he's interesting or not</param>
        /// <returns>True if the peer is interesting, false otherwise</returns>
        public override bool IsInteresting(PeerId id)
        {
            lock (this.isInterestingBuffer)     // I reuse a BitField as a buffer so i don't have to keep allocating new ones
            {
                lock (this.myBitfield)
                    Array.Copy(this.myBitfield.Array, 0, isInterestingBuffer.Array, 0, this.myBitfield.Array.Length);

                isInterestingBuffer.Not();
                isInterestingBuffer.AndFast(id.Peer.Connection.BitField);
                if (!isInterestingBuffer.AllFalseSecure())
                    return true;                            // He's interesting if he has a piece we want

                lock (this.requests)
                    return (this.requests.ContainsKey(id)); // OR if we're already requesting a piece off him.
            }
        }

        /// <summary>
        /// Creates a request message for the first available block that the peer can download
        /// </summary>
        /// <param name="id">The id of the peer to request a piece off of</param>
        /// <param name="otherPeers">The other peers that are also downloading the same torrent</param>
        /// <returns></returns>
        public override RequestMessage PickPiece(PeerId id, PeerConnectionIDCollection otherPeers)
        {
            RequestMessage message = null;

            int checkIndex = 0;
            Priority highestPriorityFound = Priority.DoNotDownload;

            lock (this.myBitfield)
            {
                // If there is already a request on this peer, try to request the next block. If the peer is choking us, then the only
                // requests that could be continued would be existing "Fast" pieces.
                if ((message = ContinueExistingRequest(id)) != null)
                    return message;

                // Then we check if there are any allowed "Fast" pieces to download
                if ((message = GetFastPiece(id)) != null)
                    return message;

                // If the peer is choking, then we can't download from them as they had no "fast" pieces for us to download
                if (id.Peer.Connection.IsChoking)
                    return null;

                // We see if the peer has suggested any pieces we should request
                if ((message = GetSuggestedPiece(id)) != null)
                    return message;

                // Now we see what pieces the peer has that we don't have and try and request one
                Buffer.BlockCopy(this.myBitfield.Array, 0, bufferBitfield.Array, 0, this.bufferBitfield.Array.Length * 4);
                this.bufferBitfield.Not();

                if (!id.Peer.IsSeeder)
                    this.bufferBitfield.AndFast(id.Peer.Connection.BitField);

                Buffer.BlockCopy(this.bufferBitfield.Array, 0, this.previousBitfield.Array, 0, this.bufferBitfield.Array.Length * 4);

                // Out of the pieces the other peer has that we want, we look for the highest priority and store it here.
                // We then keep NANDing the bitfield until we no longer have pieces with that priority. Then we can pick
                // a piece from the "highest priority" available
                highestPriorityFound = HighestPriorityAvailable(this.bufferBitfield);

                // Nothing to download. This peer may still be interesting, but we may have set files to "DoNotDownload"
                if (highestPriorityFound == Priority.DoNotDownload)
                    return null;

                for (int i = 0; i < otherPeers.Count; i++)
                {
                    lock (otherPeers[i])
                    {
                        if (otherPeers[i].Peer.Connection == null || otherPeers[i].Peer.IsSeeder)
                            continue;

                        Buffer.BlockCopy(this.bufferBitfield.Array, 0, this.previousBitfield.Array, 0, this.bufferBitfield.Array.Length * 4);
                        this.bufferBitfield.AndFast(otherPeers[i].Peer.Connection.BitField);
                        if (this.bufferBitfield.AllFalseSecure() || highestPriorityFound != HighestPriorityAvailable(this.bufferBitfield))
                            break;
                    }
                }

                // FIXME: The bitfield still contains pieces from files which are *not* the highest priority. Fix this.

                // When picking the piece, we start at a random index and then scan forwards to select the first available piece.
                // If none is found, we scan from the start up until that random index. If nothing is found, the peer is actually
                // uninteresting.
                int midPoint = random.Next(0, this.previousBitfield.Length);
                int endIndex = this.previousBitfield.Length;
                checkIndex = midPoint;

                // First we check all the places from midpoint -> end
                // Then if we dont find anything we check from 0 -> midpoint
                while ((checkIndex = this.previousBitfield.FirstTrue(checkIndex, endIndex)) != -1)
                    if (AlreadyHaveOrRequested(checkIndex))
                        checkIndex++;
                    else
                        break;

                if (checkIndex == -1)
                {
                    checkIndex = 0;
                    while ((checkIndex = this.previousBitfield.FirstTrue(checkIndex, midPoint)) != -1)
                        if (AlreadyHaveOrRequested(checkIndex))
                            checkIndex++;
                        else
                            break;
                }

                if (checkIndex == -1)
                    return null;

                // Request the piece
                else
                {
                    message = this.GenerateRequest(id, checkIndex);
                    PieceCollection reqs = requests[id];
                    for (int i = 0; i < reqs.Count; i++)
                    {
                        if (reqs[i].Index != checkIndex)
                            continue;

                        id.TorrentManager.PieceManager.RaiseBlockRequested(new BlockEventArgs(reqs[i].Blocks[0], reqs[i], id));
                    }

                    return message;
                }
            }
        }


        /// <summary>
        /// Removes any outstanding requests from the supplied peer
        /// </summary>
        /// <param name="id">The peer to remove outstanding requests from</param>
        public override void RemoveRequests(PeerId id)
        {
            lock (this.requests)
            {
                if (this.requests.ContainsKey(id))
                {
                    PieceCollection pieces = this.requests[id];
                    for (int i = 0; i < pieces.Count; i++)
                        for (int j = 0; j < pieces[i].Blocks.Length; j++)
                            if (pieces[i].Blocks[j].Requested && !pieces[i].Blocks[j].Received)
                            {
                                id.Peer.Connection.AmRequestingPiecesCount--;
                                id.TorrentManager.PieceManager.RaiseBlockRequestCancelled(new BlockEventArgs(pieces[i].Blocks[j], pieces[i], id));
                            }

                    // Should this be happening?
                    for (int i = 0; i < pieces.Count; i++)
                        this.myBitfield[pieces[i].Index] = false;

                    this.requests.Remove(id);
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="message"></param>
        private void RemoveRequests(PeerId id, RequestMessage message)
        {
            lock (this.requests)
            {
                if (!this.requests.ContainsKey(id))
                    return;

                PieceCollection pieces = this.requests[id];
                for (int i = 0; i < pieces.Count; i++)
                {
                    if (message.PieceIndex != pieces[i].Index)
                        continue;

                    for (int j = 0; j < pieces[i].Blocks.Length; j++)
                    {
                        if (pieces[i].Blocks[j].StartOffset != message.StartOffset)
                            continue;

                        if (pieces[i].Blocks[j].RequestLength != message.RequestLength)
                            throw new TorrentException("Trying to remove a request that doesn't exist");

                        if (!pieces[i].Blocks[j].Requested)
                            throw new TorrentException("The block was not requested");

                        pieces[i].Blocks[j].Requested = false;
                        id.Peer.Connection.AmRequestingPiecesCount--;
                        id.TorrentManager.PieceManager.RaiseBlockRequestCancelled(new BlockEventArgs(pieces[i].Blocks[j], pieces[i], id));

                        if (pieces[i].NoBlocksRequested)
                            pieces.RemoveAt(i);

                        if (pieces.Count == 0)
                            this.requests.Remove(id);

                        return;
                    }
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="recieveBuffer"></param>
        /// <param name="offset"></param>
        /// <param name="writeIndex"></param>
        /// <param name="p"></param>
        public override PieceEvent ReceivedPieceMessage(PeerId id, byte[] recieveBuffer, PieceMessage message)
        {
            lock (this.requests)
            {
                if (!this.requests.ContainsKey(id))
                {
                    Logger.Log("Received piece from a peer we are requesting 0 pieces off");
                    return PieceEvent.BlockNotRequested;
                }

                PieceCollection pieces = this.requests[id];

                // If we are *not* requesting the piece that this block came from, we kill the connection
                Piece piece = PiecePickerBase.GetPieceFromIndex(pieces, message.PieceIndex);
                if (piece == null)
                {
                    Logger.Log("Received block from a piece we aren't requesting");
                    return PieceEvent.BlockNotRequested;
                }

                // Pick out the block that this piece message belongs to
                int blockIndex = PiecePickerBase.GetBlockIndex(piece.Blocks, message.StartOffset, message.RequestLength);
                if (blockIndex == -1)
                {
                    Logger.Log(id, "Invalid block start offset returned");
                    return PieceEvent.BlockNotRequested;
                }

                if (piece.Blocks[blockIndex].Received)
                {
                    Logger.Log("Block already received");
                    return PieceEvent.BlockNotRequested;
                }
                //throw new MessageException("Block already received");

                if (!piece.Blocks[blockIndex].Requested)
                {
                    Logger.Log("Block was not requested");
                    return PieceEvent.BlockNotRequested;
                }
                //throw new MessageException("Block was not requested");

                piece.Blocks[blockIndex].Received = true;
                id.Peer.Connection.AmRequestingPiecesCount--;
                id.TorrentManager.PieceManager.RaiseBlockReceived(new BlockEventArgs(piece.Blocks[blockIndex], piece, id));
                lock(this.unhashedPieces)
                    this.unhashedPieces.Add(piece.Index);
                id.TorrentManager.FileManager.QueueWrite(id, recieveBuffer, message, piece, unhashedPieces);

                if (piece.AllBlocksReceived)
                {
                    pieces.Remove(piece);

                    if (pieces.Count == 0)
                        this.requests.Remove(id);
                }

                return PieceEvent.BlockWriteQueued;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        public override void ReceivedChokeMessage(PeerId id)
        {
            // If fast peer peers extensions are not supported on both sides, all pending requests are implicitly rejected
            if (!(id.Peer.Connection.SupportsFastPeer && ClientEngine.SupportsFastPeer))
            {
                this.RemoveRequests(id);
            }
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


        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="rejectRequestMessage"></param>
        public override void ReceivedRejectRequest(PeerId id, RejectRequestMessage rejectRequestMessage)
        {
            lock (this.requests)
            {
                if (!this.requests.ContainsKey(id))
                    throw new MessageException("Received reject request for a piece i'm not requesting");

                PieceCollection pieces = this.requests[id];

                Piece piece = PiecePickerBase.GetPieceFromIndex(pieces, rejectRequestMessage.PieceIndex);
                if (piece == null)
                    throw new MessageException("Received reject request for a piece i'm not requesting");

                int blockIndex = PiecePickerBase.GetBlockIndex(piece.Blocks, rejectRequestMessage.StartOffset, rejectRequestMessage.RequestLength);
                if (blockIndex == -1)
                    throw new MessageException("Received reject request for a piece i'm not requesting");

                if (!piece.Blocks[blockIndex].Requested || piece.Blocks[blockIndex].Received)
                    throw new MessageException("We didnt request this block or we already received it");

                piece.Blocks[blockIndex].Requested = false;
                id.Peer.Connection.AmRequestingPiecesCount--;
                id.TorrentManager.PieceManager.RaiseBlockRequestCancelled(new BlockEventArgs(piece.Blocks[blockIndex], piece, id));
            }
        }

        #endregion
    }
}
