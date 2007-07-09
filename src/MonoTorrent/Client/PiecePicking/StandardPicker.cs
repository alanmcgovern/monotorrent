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

        private List<BitField> previousBitfields;
        private BitField unhashedPieces;   // Store the index of finished pieces which are not hashed. These count as "AlreadyHaveOrRequested"


        private TorrentFile[] torrentFiles;
        private Dictionary<PeerIdInternal, PieceCollection> requests;
        internal Dictionary<PeerIdInternal, PieceCollection> Requests
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
                foreach (KeyValuePair<PeerIdInternal, PieceCollection> keypair in this.requests)
                    for (int i = 0; i < keypair.Value.Count; i++)
                        for (int j = 0; j < keypair.Value[i].Blocks.Length; j++)
                            if (keypair.Value[i].Blocks[j].Requested && !keypair.Value[i].Blocks[j].Received)
                                result++;
            }
            return result;
        }

        internal BitField UnhashedPieces
        {
            get { return this.unhashedPieces; }
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
            this.requests = new Dictionary<PeerIdInternal, PieceCollection>(32);
            this.priorities = (int[])Enum.GetValues(typeof(Priority));
            Array.Sort<int>(this.priorities);
            Array.Reverse(this.priorities);

            this.myBitfield = bitField;
            this.previousBitfields = new List<BitField>();
            this.previousBitfields.Add(new BitField(myBitfield.Length));
            this.torrentFiles = torrentFiles;
            this.unhashedPieces = new BitField(myBitfield.Length);
            this.requests = new Dictionary<PeerIdInternal, PieceCollection>();

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

            foreach (KeyValuePair<PeerIdInternal, PieceCollection> keypair in this.requests)
                for(int i=0; i < keypair.Value.Count; i++)
                    if (keypair.Value[i].Index == index)
                        return true;

            lock (this.unhashedPieces)
                return this.unhashedPieces[index];
        }


        private RequestMessage ContinueAnyExisting(PeerIdInternal id)
        {
            // If this peer is currently a 'dodgy' peer, then don't allow him to help with someone else's
            // piece request.
            if (id.Peer.RepeatedHashFails != 0)
                return null;

            // Otherwise, if this peer has any of the pieces that are currently being requested, try to
            // request a block from one of those pieces
            foreach (KeyValuePair<PeerIdInternal, PieceCollection> keypair in this.requests)
            {
                // If the peer who this piece is assigned to is dodgy we don't want to help download the piece
                if (keypair.Key.Peer.RepeatedHashFails != 0)
                    continue;

                for (int i = 0; i < keypair.Value.Count; i++)
                {
                    if (!id.Peer.Connection.BitField[keypair.Value[i].Index])
                        continue;

                    Piece p = keypair.Value[i];

                    if (p.AllBlocksRequested)
                        continue;
                    
                    for (int j = 0; j < p.Blocks.Length; j++)
                        if (!p.Blocks[j].Requested)
                        {
                            p.Blocks[j].Requested = true;
                            return p.Blocks[j].CreateRequest(id);
                        }
                }
            }

            return null;
        }


        /// <summary>
        /// When picking a piece, attempt to request the next free block from an existing request if there is one
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private RequestMessage ContinueExistingRequest(PeerIdInternal id)
        {
            // Return null if we aren't already tracking pieces for this peer
            if (!this.requests.ContainsKey(id))
                return null;

            // Get the list of all the pieces we're requesting off the peer and check to see if there
            // are any blocks not requested yet. If there are, request them
            PieceCollection reqs = this.requests[id];
            for (int i = 0; i < reqs.Count; i++)
            {
                if (reqs[i].AllBlocksRequested)
                    continue;

                for (int j = 0; j < reqs[i].Blocks.Length; j++)
                {
                    if (!reqs[i].Blocks[j].Requested)
                    {
                        reqs[i].Blocks[j].Requested = true;
                        return reqs[i].Blocks[j].CreateRequest(id);
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
        private RequestMessage GenerateRequest(PeerIdInternal id, int index)
        {
            if (!requests.ContainsKey(id))
                requests.Add(id, new PieceCollection(2));

            PieceCollection reqs = requests[id];

            Piece p = new Piece(index, id.TorrentManager.Torrent);
            reqs.Add(p);
            p.Blocks[0].Requested = true;
            return p.Blocks[0].CreateRequest(id);
        }


        /// <summary>
        /// When picking a piece, attempt to request a fast piece if there is one available
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private RequestMessage GetFastPiece(PeerIdInternal id)
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
        /// When picking a piece, request a new piece normally
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private RequestMessage GetStandardRequest(PeerIdInternal id, PeerIdCollection otherPeers)
        {
            int checkIndex = 0;
            BitField current = null;
            RequestMessage message = null;
            Stack<BitField> rarestFirstBitfields = GenerateRarestFirst(id, otherPeers);

            try
            {
                while (rarestFirstBitfields.Count > 0)
                {
                    current = rarestFirstBitfields.Pop();

                    // When picking the piece, we start at a random index and then scan forwards to select the first available piece.
                    // If none is found, we scan from the start up until that random index. If nothing is found, the peer is actually
                    // uninteresting.
                    int midPoint = random.Next(0, current.Length);
                    int endIndex = current.Length;
                    checkIndex = midPoint;

                    // First we check all the places from midpoint -> end
                    // Then if we dont find anything we check from 0 -> midpoint
                    while ((checkIndex = current.FirstTrue(checkIndex, endIndex)) != -1)
                        if (AlreadyHaveOrRequested(checkIndex))
                            checkIndex++;
                        else
                            break;

                    if (checkIndex == -1)
                    {
                        checkIndex = 0;
                        while ((checkIndex = current.FirstTrue(checkIndex, midPoint)) != -1)
                            if (AlreadyHaveOrRequested(checkIndex))
                                checkIndex++;
                            else
                                break;
                    }

                    if (checkIndex == -1)
                    {
                        // FIXME: This might be optimisable. I've already checked all the indices in the
                        // current bitfield and they weren't interesting, so i could NAND it against all the
                        // other bitfields so i don't recheck the same indices over and over. It should be faster
                        ClientEngine.BufferManager.FreeBitfield(ref current);
                        continue;
                    }

                    // Request the piece
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

                return null;
            }

            finally
            {
                if (current != null)
                    ClientEngine.BufferManager.FreeBitfield(ref current);

                while (rarestFirstBitfields.Count > 0)
                {
                    BitField popped = rarestFirstBitfields.Pop();
                    ClientEngine.BufferManager.FreeBitfield(ref popped);
                }
            }
        }

        private Stack<BitField> GenerateRarestFirst(PeerIdInternal id, PeerIdCollection otherPeers)
        {
            Priority highestPriority = Priority.Low;
            Stack<BitField> bitfields = new Stack<BitField>();
            BitField current = ClientEngine.BufferManager.GetBitfield(myBitfield.Length);

            try
            {
                // Copy my bitfield into the buffer and invert it so it contains a list of pieces i want
                Buffer.BlockCopy(myBitfield.Array, 0, current.Array, 0, current.Array.Length * 4);
                current.Not();

                // Fastpath - If he's a seeder, there's no point in AND'ing his bitfield as nothing will be set false
                if (!id.Peer.IsSeeder)
                    current.AndFast(id.Peer.Connection.BitField);

                // Check the priority of the availabe pieces and record the highest one found
                highestPriority = HighestPriorityAvailable(current);

                // If true, then there are no pieces to download from this peer
                if (highestPriority == Priority.DoNotDownload)
                    return bitfields;

                // Store this bitfield as the first iteration of the Rarest First algorithm.
                bitfields.Push(current);

                // Get a cloned copy of the bitfield and begin iterating to find the rarest pieces
                current = ClientEngine.BufferManager.GetClonedBitfield(current);

                for (int i = 0; i < otherPeers.Count; i++)
                {
                    lock (otherPeers[i])
                    {
                        if (otherPeers[i].Peer.Connection == null || otherPeers[i].Peer.IsSeeder)
                            continue;

                        // currentBitfield = currentBitfield & (!otherBitfield)
                        // This calculation finds the pieces this peer has that other peers *do not* have.
                        // i.e. the rarest piece.
                        current.AndNotFast(otherPeers[i].Peer.Connection.BitField);

                        // If the bitfield now has no pieces or we've knocked out a file which is at
                        // a high priority then we've completed our task
                        if (current.AllFalseSecure() || highestPriority != HighestPriorityAvailable(current))
                            break;

                        // Otherwise push the bitfield on the stack and clone it and iterate again.
                        bitfields.Push(current);
                        current = ClientEngine.BufferManager.GetClonedBitfield(current);
                    }
                }

                return bitfields;
            }
            finally
            {
                ClientEngine.BufferManager.FreeBitfield(ref current);
            }
        }


        /// <summary>
        /// When picking a piece, attempt to request a piece that the peer has recommended that we download
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private RequestMessage GetSuggestedPiece(PeerIdInternal id)
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
        public override bool IsInteresting(PeerIdInternal id)
        {
            BitField bitfield = ClientEngine.BufferManager.GetBitfield(myBitfield.Length);
            try
            {
                Buffer.BlockCopy(myBitfield.Array, 0, bitfield.Array, 0, myBitfield.Array.Length * 4);

                bitfield.Not();
                bitfield.AndFast(id.Peer.Connection.BitField);
                if (!bitfield.AllFalseSecure())
                    return true;                            // He's interesting if he has a piece we want

                lock (this.requests)
                    return (this.requests.ContainsKey(id)); // OR if we're already requesting a piece off him.
            }
            finally
            {
                ClientEngine.BufferManager.FreeBitfield(ref bitfield);
            }
        }

        /// <summary>
        /// Creates a request message for the first available block that the peer can download
        /// </summary>
        /// <param name="id">The id of the peer to request a piece off of</param>
        /// <param name="otherPeers">The other peers that are also downloading the same torrent</param>
        /// <returns></returns>
        public override RequestMessage PickPiece(PeerIdInternal id, PeerIdCollection otherPeers)
        {
            RequestMessage message = null;
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

                if ((message = ContinueAnyExisting(id)) != null)
                    return message;

                // We see if the peer has suggested any pieces we should request
                if ((message = GetSuggestedPiece(id)) != null)
                    return message;

                // Now we see what pieces the peer has that we don't have and try and request one
                return GetStandardRequest(id, otherPeers);
            }
        }


        /// <summary>
        /// Removes any outstanding requests from the supplied peer
        /// </summary>
        /// <param name="id">The peer to remove outstanding requests from</param>
        public override void RemoveRequests(PeerIdInternal id)
        {
            lock (this.requests)
            {
                foreach (Piece p in AllPieceRequests())
                {
                    for (int i = 0; i < p.Blocks.Length; i++)
                    {
                        if (p.Blocks[i].Requested && !p.Blocks[i].Received && id.Equals(p.Blocks[i].RequestedOff))
                        {
                            p.Blocks[i].Requested = false;
                            p.Blocks[i].RequestedOff = null;
                            id.Peer.Connection.AmRequestingPiecesCount--;
                            id.TorrentManager.PieceManager.RaiseBlockRequestCancelled(new BlockEventArgs(p.Blocks[i], p, id));
                        }
                    }
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="message"></param>
        private void RemoveRequests(PeerIdInternal id, RequestMessage message)
        {
            lock (this.requests)
            {
                foreach (Piece p in AllPieceRequests())
                {
                    if (p.Index != message.PieceIndex)
                        continue;

                    int blockIndex = PiecePickerBase.GetBlockIndex(p.Blocks, message.StartOffset, message.RequestLength);
                    if (blockIndex != -1)
                    {
                        if (p.Blocks[blockIndex].Requested && !p.Blocks[blockIndex].Received && id.Equals(p.Blocks[blockIndex].RequestedOff))
                        {
                            p.Blocks[blockIndex].Requested = false;
                            p.Blocks[blockIndex].RequestedOff = null;
                            id.Peer.Connection.AmRequestingPiecesCount--;
                            id.TorrentManager.PieceManager.RaiseBlockRequestCancelled(new BlockEventArgs(p.Blocks[blockIndex], p, id));
                            return;
                        }
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
        public override PieceEvent ReceivedPieceMessage(PeerIdInternal id, ArraySegment<byte> recieveBuffer, PieceMessage message)
        {
            lock (this.requests)
            {
                Piece piece = null;
                KeyValuePair<PeerIdInternal, PieceCollection>? owner = null;
                foreach(KeyValuePair<PeerIdInternal, PieceCollection> keypair in this.requests)
                    foreach (Piece p in keypair.Value)
                        if (p.Index == message.PieceIndex)
                        {
                            owner = keypair;
                            piece = p;
                            break;
                        }

                if (piece == null)
                {
                    Logger.Log("Received block from a piece we aren't requesting");
                    return PieceEvent.BlockNotRequested;
                }

                // Pick out the block that this piece message belongs to
                int blockIndex = PiecePickerBase.GetBlockIndex(piece.Blocks, message.StartOffset, message.RequestLength);
                if (blockIndex == -1 || !id.Equals(piece.Blocks[blockIndex].RequestedOff))
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
                id.TorrentManager.FileManager.QueueWrite(id, recieveBuffer, message, piece);

                if (piece.AllBlocksReceived)
                {
#warning review usage of the unhashedpieces variable
                    lock (this.unhashedPieces)
                        if (!this.myBitfield[piece.Index])
                            this.unhashedPieces[piece.Index] = true;

                    owner.Value.Value.Remove(piece);
                    if (owner.Value.Value.Count == 0)
                        this.requests.Remove(owner.Value.Key);
                }

                return PieceEvent.BlockWriteQueued;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        public override void ReceivedChokeMessage(PeerIdInternal id)
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
                    if ((message = id.Peer.Connection.Dequeue()) is RequestMessage)
                        RemoveRequests(id, (RequestMessage)message);
                    else
                        id.Peer.Connection.Enqueue(message);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="rejectRequestMessage"></param>
        public override void ReceivedRejectRequest(PeerIdInternal id, RejectRequestMessage rejectRequestMessage)
        {
            lock (this.requests)
            {
                foreach (Piece p in AllPieceRequests())
                {
                    if (p.Index != rejectRequestMessage.PieceIndex)
                        continue;

                    int blockIndex = PiecePickerBase.GetBlockIndex(p.Blocks, rejectRequestMessage.StartOffset, rejectRequestMessage.RequestLength);
                    if (blockIndex == -1)
                        return;

                    if (!p.Blocks[blockIndex].Received && id.Equals(p.Blocks[blockIndex].RequestedOff))
                    {
                        p.Blocks[blockIndex].RequestedOff = null;
                        p.Blocks[blockIndex].Requested = false;
                        id.Peer.Connection.AmRequestingPiecesCount--;
                        id.TorrentManager.PieceManager.RaiseBlockRequestCancelled(new BlockEventArgs(p.Blocks[blockIndex], p, id));
                    }
                }
            }
        }


        public override void Reset()
        {
            this.unhashedPieces.SetAll(false);
            this.requests.Clear();
        }


        public IEnumerable<Piece> AllPieceRequests()
        {
            foreach (PieceCollection pieces in this.requests.Values)
                foreach (Piece p in pieces)
                    yield return p;
        }
        #endregion
    }
}
